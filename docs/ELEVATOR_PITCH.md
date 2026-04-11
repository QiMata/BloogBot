# Westworld of Warcraft — Elevator Pitch (10 min)

## Opening Hook (30 seconds)

What if you could populate a World of Warcraft server with up to 3,000 AI characters — each with a distinct personality, each making autonomous decisions, each indistinguishable from a real player — and then use that world as a laboratory for studying human social behavior at scale? That's Westworld of Warcraft.

We've built a platform that creates autonomous AI agents inside MMO game worlds. They quest, trade, form groups, run dungeons, and fight battlegrounds. But the real power isn't just population simulation — it's that we've created a controllable, observable environment for running experiments on emergent social and economic behavior that would be impossible or unethical to conduct in the real world.

---

## The Problem (1 minute)

In 2005, a glitch in World of Warcraft caused a virtual plague — the Corrupted Blood incident. A debuff from the raid boss Hakkar the Soulflayer spread uncontrollably through cities, killing low-level characters, turning populated areas into ghost towns. Players panicked, fled to the wilderness, or deliberately spread the infection. Epidemiologists at the CDC and researchers at Tufts University studied it as an unplanned model of real-world pandemic behavior — because the social dynamics were authentic. Real people made real decisions under pressure.

That was an accident. What if you could do it on purpose?

MMO game worlds are extraordinary social laboratories. They have functioning economies with supply chains, scarcity, and speculation. They have cooperative institutions — guilds, raid teams, trade networks. They have conflict, competition, and emergent social hierarchies. Researchers have long recognized this, but running controlled experiments requires something the field has never had: a population of believable autonomous agents whose parameters you can control.

Game AI hasn't been up to the task. Bots follow scripts. NPCs walk patrol routes. No existing system produces agents that participate in an MMO economy, form social groups, and respond to systemic shocks the way humans do.

Until now.

---

## What We Built (3 minutes)

Westworld of Warcraft is a layered microservices platform that creates autonomous AI agents inside World of Warcraft 1.12.1 — the original vanilla game that defined the MMO genre. We chose vanilla WoW because it's a fully understood, stable environment with a rich private server ecosystem (VMaNGOS), making it an ideal sandbox for AI research.

### Population at Scale

Westworld of Warcraft populates a game server with up to 3,000 autonomous AI agents. They quest, farm, craft, trade on the auction house, form groups for dungeons, and queue for battlegrounds. From the server's perspective — and from any real player's perspective — they're indistinguishable from human players.

But the platform isn't just about filling seats. Each agent's personality and economic behavior are parameterized. You can configure a population where 30% of agents are resource hoarders and 70% trade cooperatively — then run the same scenario with the ratios inverted — and observe what happens to market prices, material availability, and guild formation. You can introduce artificial scarcity of a key crafting material and watch whether agents form cooperative trade networks or engage in price manipulation. You can simulate a server-wide crisis and study evacuation patterns, information spread, and collective decision-making.

### How It Works Under the Hood

Our agents connect to the game server using a pure C# reimplementation of the entire WoW network protocol. No game client needed. Each headless agent authenticates via SRP6 cryptographic handshake, negotiates packet encryption, and speaks the exact same binary protocol as a real WoW client — connecting to the same server as real players. We've validated this against 937+ packet codec tests across three WoW expansions.

This is the scalable path. One machine can run dozens of headless agents simultaneously. We've tested 10-bot raid groups clearing dungeons and 20-bot teams coordinating Warsong Gulch battlegrounds — all headless, all protocol-accurate.

We also have a secondary mode that injects directly into the real WoW game client for ground-truth validation. This gives us direct memory access and real-time packet capture — everything the real client sees, our agent sees. We use this to verify that our headless agents behave identically to characters running through the actual game client.

### Custom Physics Engine

Because our background agents don't have the game client's physics, we built our own. A 6,600-line C++ physics engine implements the PhysX Character Controller Toolkit pattern: three-pass movement (up/side/down sweeps), capsule collision against the game's terrain and building geometry, gravity at WoW's exact 19.29 yd/s², step-up for stairs, ground snapping, swimming, and falling. We calibrate it against recordings from the real client to achieve movement parity — our headless bots walk through the world identically to a real player.

### 27 Class Profiles

Every class and specialization in vanilla WoW has a complete combat behavior profile: Warriors (Arms, Fury, Protection), Rogues, Hunters, Druids, Paladins, Priests, Shamans, Mages, and Warlocks — 27 specs total. Each profile implements situational combat rotations, resource management (mana, rage, energy), buff priority, healing triage, and target selection. These aren't simple macro scripts — they're behavior trees that adapt to combat state in real time.

---

## The AI Layer (3 minutes)

Here's where it gets interesting. The platform we just described is the body. Now let's talk about the brain.

### Personality-Driven Behavior

Every agent has a personality defined by the Big Five psychological traits: Openness, Conscientiousness, Extraversion, Agreeableness, and Neuroticism. These aren't cosmetic labels. They modulate decision-making at every level.

A high-Conscientiousness warrior executes rotations with machine precision. A high-Extraversion rogue takes aggressive flanking positions. A high-Neuroticism healer plays conservatively, holding cooldowns for emergencies. A high-Agreeableness tank prioritizes party survival over personal damage.

The goal is emergent behavior — not programming specific actions, but setting personality parameters and watching distinct playstyles emerge from the same underlying behavior trees. Just like the hosts in Westworld, each agent is the same architecture with different drives.

### ML Decision Engine

Our DecisionEngineService uses ML.NET to train multiclass classifiers on game state snapshots. Every tick, the game state — health, mana, target distance, threat levels, party composition, nearby enemies — is captured as a protobuf ActivitySnapshot. These snapshots feed an SDCA Maximum Entropy model that learns which actions lead to success in which contexts.

The model improves through feedback: successful actions reinforce their weights, failures reduce them. Over time, the decision engine develops preferences tuned to the specific game situations it encounters. A bot that repeatedly wipes on a dungeon boss learns to adjust its strategy.

### LLM Integration

The PromptHandlingService connects our agents to large language models — Ollama for local inference, or OpenAI and Azure for cloud models. This enables a qualitatively different kind of intelligence:

- **Intent parsing**: Understanding complex multi-step objectives ("clear the dungeon, prioritize casters, save cooldowns for the boss")
- **GM command construction**: Generating valid server commands from natural language
- **Skill prioritization**: Deciding which abilities to train at each level based on spec and playstyle
- **Tactical reasoning**: Analyzing battleground situations and suggesting team strategies

The LLM doesn't replace the behavior tree — it augments it. The behavior tree handles frame-by-frame execution (dodge this spell, heal that player). The LLM handles strategic reasoning (we should push the flag room now, their healers are down).

### Multi-Agent Coordination

The WoWStateManager orchestrates everything. It tracks every agent's state via Protobuf IPC, manages the BattlegroundCoordinator for synchronized PvP, and coordinates dungeon encounters with role-aware sequencing (tank pulls, healer pre-heals, DPS executes).

We've demonstrated 10-bot Ragefire Chasm dungeon clears with a foreground tank and 9 background DPS/heals. We've tested 20-bot Warsong Gulch matches — 5v5 Horde vs Alliance, queuing, entering, and fighting in the battleground. The .NET Aspire dashboard provides real-time visibility into every agent's state, service health, and coordination progress.

---

## Why It Matters (1.5 minutes)

This isn't just a game project. It's infrastructure for studying complex social systems.

**For social science and economics**: WoW's economy is structurally similar to real economies — it has production, trade, scarcity, speculation, and institutional trust. With 3,000 parameterized agents, researchers can run controlled experiments that are impossible in the real world. What happens to a market economy when 10% of participants begin hoarding a critical resource? How do cooperative trade networks form under scarcity versus abundance? Does a population with high-Agreeableness agents develop different economic structures than one with high-Extraversion agents? These are questions about capitalism, socialism, and human nature — tested in a system where you can reset the world and run it again with different parameters.

**For epidemiology and crisis response**: The Corrupted Blood incident proved that virtual worlds produce authentic behavioral responses to systemic threats. With Westworld of Warcraft, you don't have to wait for accidents. You can engineer a plague, a resource collapse, or a factional conflict and study how populations with specific personality distributions respond — information spread, panic behavior, cooperative versus selfish strategies, institutional resilience.

**For AI research**: WoW is an extraordinarily rich testbed for multi-agent intelligence. It has long-horizon planning (quest chains spanning hours), real-time tactical decisions (PvP combat in milliseconds), cooperative scenarios (5-40 player raids), economic systems, and a vast spatial environment. Our platform provides all of this with up to 3,000 concurrent agents, comprehensive test infrastructure, and both ground-truth and simulated execution modes.

**For game development**: The techniques we're building — personality-driven NPCs, LLM-augmented decision making, learned combat behavior, coordinated multi-agent tactics — are directly applicable to next-generation game AI. Imagine MMO worlds where every character has genuine agency, forms relationships, and participates meaningfully in the game's economy and social fabric.

---

## The End State (1 minute)

The vision is two things at once.

**For players**: A WoW server that feels like 2006 — bustling cities, a functioning economy, groups forming for every dungeon, battlegrounds popping around the clock. A server administrator configures 3,000 AI agents across both factions. They spread across the world organically — questing in the Barrens, farming herbs in Felwood, clearing Scarlet Monastery, queuing for Warsong Gulch. The auction house fills with reasonably priced materials. A real player types "LFG BRD" and within minutes an AI healer and two AI DPS whisper them. The dungeon feels normal — the AI characters make tactical decisions, communicate, and occasionally make mistakes. Just like real players.

**For researchers**: A configurable social simulation engine. Spin up a world with 3,000 agents parameterized across personality, economic strategy, and social tendency. Run a resource scarcity event. Observe whether agents with high Agreeableness form cooperative distribution networks while high-Extraversion agents attempt market cornering. Reset the world. Adjust parameters. Run it again. Publish findings on emergent economic behavior that would take years of real-world observation to gather — produced in hours with full data capture.

Some agents are cautious explorers who stick to safe zones. Others are aggressive PvPers who hunt in packs. A meticulous crafter corners the market on potions. An anxious healer earns a reputation as the server's best because their neuroticism makes them obsessively attentive to health bars. These behaviors aren't scripted — they emerge from personality parameters interacting with game systems.

The world feels alive because, from every measurable perspective, it is. And every interaction is logged, parameterized, and reproducible.

---

## Current Status & What's Next (30 seconds)

Today we have:
- Complete dual-execution platform (FG injection + BG headless)
- 937+ validated protocol tests across 3 WoW versions
- 27 class/spec combat profiles
- Custom physics engine with client parity
- ML decision engine and LLM integration
- Coordinated battlegrounds (20 bots) and dungeons (10 bots)
- 490+ test files with comprehensive live validation

Next milestones:
- Personality trait modulation of behavior trees (Big Five → emergent playstyle)
- Reinforcement learning from ActivitySnapshot training data
- 40-bot Alterac Valley coordination
- LLM-driven strategic reasoning and battleground tactics
- Auction house economic simulation (crafting, farming, pricing strategies)
- Scale testing: 500 → 1,000 → 3,000 concurrent agents
- Experiment framework: parameterized population configs, event injection, data export

We're building the Westworld of Warcraft. The hosts are waking up — and this time, we're watching what they do.
