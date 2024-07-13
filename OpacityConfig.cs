using System;
using Terraria.ModLoader.Config;

namespace OpacityController;

internal class OpacityConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ClientSide;

    [Range(0, 1)]
    public float SelfOpacity { get; set; }

    [Range(0, 1)]
    public float FriendlyOpacity { get; set; }

    [Range(0, 1)]
    public float NPCOpacity { get; set; }
}
