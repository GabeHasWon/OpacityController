using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;
using System.Reflection;
using System.Collections.Generic;

namespace OpacityController;

public class OpacityController : Mod
{
    internal static bool HideFriendly = false;
    internal static bool OnlyMyPlayer = false;
    internal static bool DrawOnlyUndrawn = false;
    internal static bool DrawUnconditionally = false;

    private static readonly HashSet<int> drawnProjectiles = [];
    private static PropertyInfo rtUsagePropertyInfo = null;

    public RenderTarget2D projTarget;

    public override void Load()
    {
        if (Main.dedServ)
            return;

        Main.RunOnMainThread(() => projTarget = new RenderTarget2D(Main.graphics.GraphicsDevice, Main.screenWidth, Main.screenHeight));
        Main.OnResolutionChanged += UpdateRT;

        On_Main.DrawProjectiles += TargetProjectiles;
        On_Main.DrawProj_Inner += HideUnwantedProjectiles;
        On_Main.DrawCachedProjs += On_Main_DrawCachedProjs;

        rtUsagePropertyInfo = typeof(RenderTarget2D).GetProperty(nameof(RenderTarget2D.RenderTargetUsage));
    }

    private void On_Main_DrawCachedProjs(On_Main.orig_DrawCachedProjs orig, Main self, List<int> projCache, bool startSpriteBatch)
    {
        if (ModContent.GetInstance<OpacityConfig>().IgnoreCached)
        {
            DrawUnconditionally = true;
            orig(self, projCache, startSpriteBatch);
            return;
        }

        DrawUnconditionally = false;
        drawnProjectiles.Clear();

        rtUsagePropertyInfo.SetValue(Main.screenTarget, RenderTargetUsage.PreserveContents);
        var bindings = Main.graphics.GraphicsDevice.GetRenderTargets();

        // Friendly, own
        Main.graphics.GraphicsDevice.SetRenderTarget(projTarget);
        Main.graphics.GraphicsDevice.Clear(Color.Transparent);

        if (!startSpriteBatch)
            Main.spriteBatch.End();

        OnlyMyPlayer = true;
        HideFriendly = false;

        orig(self, projCache, true);

        Main.graphics.GraphicsDevice.SetRenderTargets(bindings);
        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null);

        float otherOpacity = ModContent.GetInstance<OpacityConfig>().SelfOpacity;
        Main.spriteBatch.Draw(projTarget, Vector2.Zero, null, Color.White * otherOpacity, 0f, Vector2.Zero, 1, SpriteEffects.None, 0);

        Main.spriteBatch.End();

        // Friendly, other
        Main.graphics.GraphicsDevice.SetRenderTarget(projTarget);
        Main.graphics.GraphicsDevice.Clear(Color.Transparent);

        OnlyMyPlayer = false;
        HideFriendly = false;

        orig(self, projCache, true);

        Main.graphics.GraphicsDevice.SetRenderTargets(bindings);
        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null);

        otherOpacity = ModContent.GetInstance<OpacityConfig>().OtherFriendlyOpacity;
        Main.spriteBatch.Draw(projTarget, Vector2.Zero, null, Color.White * otherOpacity, 0f, Vector2.Zero, 1, SpriteEffects.None, 0);

        Main.spriteBatch.End();

        // Hostile, other
        Main.graphics.GraphicsDevice.SetRenderTarget(projTarget);
        Main.graphics.GraphicsDevice.Clear(Color.Transparent);

        HideFriendly = true;

        orig(self, projCache, true);

        Main.graphics.GraphicsDevice.SetRenderTargets(bindings);
        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null);

        otherOpacity = ModContent.GetInstance<OpacityConfig>().NPCOpacity;
        Main.spriteBatch.Draw(projTarget, Vector2.Zero, null, Color.White * otherOpacity, 0f, Vector2.Zero, 1, SpriteEffects.None, 0);

        Main.spriteBatch.End();

        // All other
        Main.graphics.GraphicsDevice.SetRenderTarget(projTarget);
        Main.graphics.GraphicsDevice.Clear(Color.Transparent);

        DrawOnlyUndrawn = true;

        orig(self, projCache, true);

        Main.graphics.GraphicsDevice.SetRenderTargets(bindings);
        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null);

        otherOpacity = ModContent.GetInstance<OpacityConfig>().OtherOpacity;
        Main.spriteBatch.Draw(projTarget, Vector2.Zero, null, Color.White * otherOpacity, 0f, Vector2.Zero, 1, SpriteEffects.None, 0);

        Main.spriteBatch.End();

        rtUsagePropertyInfo.SetValue(Main.screenTarget, RenderTargetUsage.DiscardContents);

        if (!startSpriteBatch)
            Main.spriteBatch.Begin(0, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

        drawnProjectiles.Clear();
        DrawUnconditionally = true;
    }

    private void HideUnwantedProjectiles(On_Main.orig_DrawProj_Inner orig, Main self, Projectile proj)
    {
        if (DrawUnconditionally)
        {
            drawnProjectiles.Add(proj.whoAmI);
            orig(self, proj);
            return;
        }

        if (DrawOnlyUndrawn && !drawnProjectiles.Contains(proj.whoAmI))
        {
            drawnProjectiles.Add(proj.whoAmI);
            orig(self, proj);
            return;
        }

        if (HideFriendly && proj.friendly && !proj.hostile)
        {
            return;
        }

        if (!HideFriendly && (OnlyMyPlayer && Main.myPlayer != proj.owner || !OnlyMyPlayer && proj.owner == Main.myPlayer))
        {
            return;
        }

        if (!HideFriendly && proj.hostile)
            return;

        drawnProjectiles.Add(proj.whoAmI);
        orig(self, proj);
    }

    private void UpdateRT(Vector2 size) => Main.RunOnMainThread(() => projTarget = new RenderTarget2D(Main.graphics.GraphicsDevice, (int)size.X, (int)size.Y));

    private void TargetProjectiles(On_Main.orig_DrawProjectiles orig, Main self)
    {
        drawnProjectiles.Clear();
        DrawUnconditionally = false;

        rtUsagePropertyInfo.SetValue(Main.screenTarget, RenderTargetUsage.PreserveContents);
        DrawFriendly(orig, self, true);
        DrawFriendly(orig, self, false);
        DrawHostile(orig, self);
        DrawAllRemainingProjectiles(orig, self);

        rtUsagePropertyInfo.SetValue(Main.screenTarget, RenderTargetUsage.DiscardContents);

        DrawUnconditionally = true;
        drawnProjectiles.Clear();
    }

    private void DrawAllRemainingProjectiles(On_Main.orig_DrawProjectiles orig, Main self)
    {
        var bindings = Main.graphics.GraphicsDevice.GetRenderTargets();
        Main.graphics.GraphicsDevice.SetRenderTarget(projTarget);
        Main.graphics.GraphicsDevice.Clear(Color.Transparent);

        DrawOnlyUndrawn = true;
        orig(self);
        DrawOnlyUndrawn = false;

        Main.graphics.GraphicsDevice.SetRenderTargets(bindings);
        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null);

        float otherOpacity = ModContent.GetInstance<OpacityConfig>().OtherOpacity;
        Main.spriteBatch.Draw(projTarget, Vector2.Zero, null, Color.White * otherOpacity, 0f, Vector2.Zero, 1, SpriteEffects.None, 0);

        Main.spriteBatch.End();
    }

    private void DrawHostile(On_Main.orig_DrawProjectiles orig, Main self)
    {
        var bindings = Main.graphics.GraphicsDevice.GetRenderTargets();
        Main.graphics.GraphicsDevice.SetRenderTarget(projTarget);
        Main.graphics.GraphicsDevice.Clear(Color.Transparent);

        HideFriendly = true;
        orig(self);

        Main.graphics.GraphicsDevice.SetRenderTargets(bindings);
        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null);

        float enemyOpacity = ModContent.GetInstance<OpacityConfig>().NPCOpacity;
        Main.spriteBatch.Draw(projTarget, Vector2.Zero, null, Color.White * enemyOpacity, 0f, Vector2.Zero, 1, SpriteEffects.None, 0);

        Main.spriteBatch.End();
    }

    private void DrawFriendly(On_Main.orig_DrawProjectiles orig, Main self, bool onlyMine)
    {
        var bindings = Main.graphics.GraphicsDevice.GetRenderTargets();
        Main.graphics.GraphicsDevice.SetRenderTarget(projTarget);
        Main.graphics.GraphicsDevice.Clear(Color.Transparent);

        OnlyMyPlayer = onlyMine;
        HideFriendly = false;

        orig(self);

        Main.graphics.GraphicsDevice.SetRenderTargets(bindings);
        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null);

        var config = ModContent.GetInstance<OpacityConfig>();
        float opacity = !onlyMine ? config.OtherFriendlyOpacity : config.SelfOpacity;
        Main.spriteBatch.Draw(projTarget, Vector2.Zero, null, Color.White * opacity, 0f, Vector2.Zero, 1, SpriteEffects.None, 0);

        Main.spriteBatch.End();
    }
}
