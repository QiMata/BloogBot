﻿using RaidMemberBot.Client;
using RaidMemberBot.Game;
using RaidMemberBot.Game.Statics;
using RaidMemberBot.Mem;
using RaidMemberBot.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using static RaidMemberBot.Constants.Enums;

namespace RaidMemberBot.AI.SharedStates
{
    public abstract class CombatRotationTask : BotTask
    {
        Position hostileTargetLastPosition;

        bool backpedaling;
        int backpedalStartTime;

        public WoWUnit raidLeader;
        public CombatRotationTask(IClassContainer container, Stack<IBotTask> botTasks) : base(container, botTasks, TaskType.Combat)
        {
            raidLeader = ObjectManager.PartyMembers.First(x => x.Guid == ObjectManager.PartyLeaderGuid);
            Container.State.Action = "Starting combat rotation";
        }

        public abstract void PerformCombatRotation();

        public bool Update(int desiredRange)
        {
            if (ObjectManager.Player.Target == null) return true;

            if (!Container.State.IsMainTank && ObjectManager.Aggressors.Any(x => x.TargetGuid == ObjectManager.Player.Guid))
            {
                MoveTowardsTank();
                return true;
            }

            hostileTargetLastPosition = ObjectManager.Player.Target.Position;
            // melee classes occasionally end up in a weird state where they are too close to hit the mob,
            // so we backpedal a bit to correct the position
            if (backpedaling && Environment.TickCount - backpedalStartTime > 500)
            {
                ObjectManager.Player.StopMovement(ControlBits.Back);
                backpedaling = false;
            }
            if (backpedaling)
                return true;

            // the server-side los check is broken on Kronos, so we have to rely on an error message on the client.
            // when we see it, move toward the unit a bit to correct the position.
            if (!ObjectManager.Player.InLosWith(ObjectManager.Player.Target) || ObjectManager.Player.Position.DistanceTo(ObjectManager.Player.Target.Position) > desiredRange)
            {
                if (ObjectManager.Player.Position.DistanceTo(ObjectManager.Player.Target.Position) <= desiredRange)
                {
                    ObjectManager.Player.StopAllMovement();

                    ObjectManager.Player.Face(ObjectManager.Player.Target.Position);
                }
                else
                {
                    Position[] locations = NavigationClient.Instance.CalculatePath(ObjectManager.MapId, ObjectManager.Player.Position, ObjectManager.Player.Target.GetPointBehindUnit(3), true);

                    ObjectManager.Player.MoveToward(locations[1]);
                    return true;
                }
            }
            else
            {
                ObjectManager.Player.StopAllMovement();

                // ensure we're facing the target
                if (!ObjectManager.Player.IsFacing(ObjectManager.Player.Target.Position))
                    ObjectManager.Player.Face(ObjectManager.Player.Target.Position);

                // make sure casters don't move or anything while they're casting by returning here
                if ((ObjectManager.Player.IsCasting || ObjectManager.Player.IsChanneling) && ObjectManager.Player.Class != Class.Warrior && ObjectManager.Player.Class != Class.Rogue)
                    return true;
            }

            return false;
        }

        public bool MoveTowardsTank()
        {
            if (raidLeader.Position.DistanceTo(ObjectManager.Player.Position) > 5)
            {
                Container.State.Action = "Moving towards tank";
                Position[] locations = NavigationClient.Instance.CalculatePath(ObjectManager.MapId, ObjectManager.Player.Position, raidLeader.Position, true);

                ObjectManager.Player.MoveToward(locations[1]);
                return true;
            }
            else
            {
                Container.State.Action = "In position near tank";
                ObjectManager.Player.StopAllMovement();
                return false;
            }
        }

        public bool MoveBehindTarget(float distance)
        {
            if (ObjectManager.Player.Target == null) return true;

            if (ObjectManager.Player.IsBehind(ObjectManager.Player.Target)
                && ObjectManager.Player.Position.DistanceTo(ObjectManager.Player.Target.Position) < distance + 1
                && ObjectManager.Player.Position.DistanceTo(ObjectManager.Player.Target.Position) > distance - 1)
            {
                return false;
            }

            Container.State.Action = "Moving behind target";
            Position[] locations = NavigationClient.Instance.CalculatePath(ObjectManager.MapId, ObjectManager.Player.Position, ObjectManager.Player.Target.GetPointBehindUnit(distance), true);

            ObjectManager.Player.MoveToward(locations[1]);
            return true;
        }
        public bool MoveBehindTankSpot(float distance)
        {
            if (distance < 3)
            {
                ObjectManager.Player.StopAllMovement();
                return false;
            }

            Position tankPosition = new Position(Container.State.TankPosition.X, Container.State.TankPosition.Y, Container.State.TankPosition.Z);
            Position position = GetPointBehindPosition(tankPosition, Container.State.TankFacing, distance);
            Position[] locations = NavigationClient.Instance.CalculatePath(ObjectManager.MapId, ObjectManager.Player.Position, position, true);

            if (!tankPosition.InLosWith(position) || locations.Length < 2 || !locations[1].InLosWith(position))
            {
                return MoveBehindTankSpot(distance - 1);
            }

            if (ObjectManager.Player.IsBehind(tankPosition, Container.State.TankFacing)
                && ObjectManager.Player.Position.DistanceTo(tankPosition) < distance + 1
                && ObjectManager.Player.Position.DistanceTo(tankPosition) > distance - 1)
            {
                ObjectManager.Player.StopAllMovement();
                return false;
            }

            Container.State.Action = "Moving behind tank spot";
            ObjectManager.Pet?.FollowPlayer();

            ObjectManager.Player.MoveToward(locations[1]);
            return true;
        }

        private Position GetPointBehindPosition(Position position, float facing, float parDistanceToMove)
        {
            var newX = position.X + parDistanceToMove * (float)-Math.Cos(facing);
            var newY = position.Y + parDistanceToMove * (float)-Math.Sin(facing);
            var end = new Position(newX, newY, position.Z);

            return end;
        }

        public bool MoveTowardsTarget()
        {
            if (!ObjectManager.Player.InLosWith(ObjectManager.Player.Target))
            {
                Container.State.Action = "Moving towards target";
                Position[] locations = NavigationClient.Instance.CalculatePath(ObjectManager.MapId, ObjectManager.Player.Position, ObjectManager.Player.Target.Position, true);

                ObjectManager.Player.MoveToward(locations[1]);
                return true;
            }
            else
            {
                Container.State.Action = "Have LOS of the target";
                ObjectManager.Player.StopAllMovement();
                return false;
            }
        }

        public bool TargetMovingTowardPlayer =>
            hostileTargetLastPosition != null &&
            hostileTargetLastPosition.DistanceTo(ObjectManager.Player.Position) > ObjectManager.Player.Target.Position.DistanceTo(ObjectManager.Player.Position);

        public bool TargetIsFleeing =>
            hostileTargetLastPosition != null &&
            hostileTargetLastPosition.DistanceTo(ObjectManager.Player.Position) < ObjectManager.Player.Target.Position.DistanceTo(ObjectManager.Player.Position);

        public void TryCastSpell(string name, int minRange, int maxRange, bool condition = true, Action callback = null, bool castOnSelf = false) =>
            TryCastSpellInternal(name, minRange, maxRange, condition, callback, castOnSelf);

        public void TryCastSpell(string name, bool condition = true, Action callback = null, bool castOnSelf = false) =>
            TryCastSpellInternal(name, 0, int.MaxValue, condition, callback, castOnSelf);

        void TryCastSpellInternal(string name, int minRange, int maxRange, bool condition = true, Action callback = null, bool castOnSelf = false)
        {
            if (ObjectManager.Player.Target == null) return;

            float distanceToTarget = ObjectManager.Player.Position.DistanceTo(ObjectManager.Player.Target.Position);

            if (ObjectManager.Player.IsSpellReady(name)
                && distanceToTarget >= minRange
                && distanceToTarget <= maxRange
                && condition
                && !ObjectManager.Player.IsStunned
                && ((!ObjectManager.Player.IsCasting && !ObjectManager.Player.IsChanneling) || ObjectManager.Player.Class == Class.Warrior)
                && Wait.For("GlobalCooldown", 1000, true))
            {
                Functions.LuaCall($"CastSpellByName('{name}')");
                callback?.Invoke();
            }
        }

        // shared by 
        public void TryUseAbility(string name, int requiredResource = 0, bool condition = true, Action callback = null)
        {
            int playerResource = 0;

            if (ObjectManager.Player.Class == Class.Warrior)
                playerResource = ObjectManager.Player.Rage;
            else if (ObjectManager.Player.Class == Class.Rogue)
                playerResource = ObjectManager.Player.Energy;
            // todo: feral druids (bear/cat form)

            if (ObjectManager.Player.IsSpellReady(name) && playerResource >= requiredResource && condition && !ObjectManager.Player.IsStunned && !ObjectManager.Player.IsCasting)
            {
                Functions.LuaCall($"CastSpellByName('{name}')");
                callback?.Invoke();
            }
        }

        // https://vanilla-wow.fandom.com/wiki/API_CastSpell
        // The id is counted from 1 through all spell types (tabs on the right side of SpellBookFrame).
        public void TryUseAbilityById(string name, int id, int requiredRage = 0, bool condition = true, Action callback = null)
        {
            if (ObjectManager.Player.IsSpellReady(name) && ObjectManager.Player.Rage >= requiredRage && condition && !ObjectManager.Player.IsStunned && !ObjectManager.Player.IsCasting)
            {
                Functions.LuaCall($"CastSpell({id}, 'spell')");
                callback?.Invoke();
            }
        }

        public bool TargetIsHostile()
        {
            if (ObjectManager.Player.TargetGuid == 0)
                return false;
            return ObjectManager.Aggressors.Any(x => x.Guid == ObjectManager.Player.TargetGuid);
        }

        public void AssignDPSTarget()
        {
            ObjectManager.Player.SetTarget(ObjectManager.SkullTargetGuid);
        }
    }
}
