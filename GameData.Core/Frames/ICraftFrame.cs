﻿namespace GameData.Core.Frames
{
    public interface ICraftFrame
    {
        bool IsOpen { get; }
        bool HasMaterialsNeeded(int slot);
        void Craft(int slot);
    }
}
