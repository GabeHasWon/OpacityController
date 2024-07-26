using System;
using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace OpacityController;

internal class OpacityConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ClientSide;

    [Range(0, 1)]
    [DefaultValue(1)]
    public float OtherFriendlyOpacity { get; set; }

    [Range(0, 1)]
    [DefaultValue(1)]
    public float SelfOpacity { get; set; }

    [Range(0, 1)]
    [DefaultValue(1)]
    public float NPCOpacity { get; set; }

    [Range(0, 1)]
    [DefaultValue(1)]
    public float OtherOpacity { get; set; }
}
