// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Sample.Web;

using System;
using System.IO;
using System.Runtime.InteropServices.JavaScript;
using System.Text;

using RedBolt.Bindings.Diligent;
using RedBolt.Bindings.Lz4;
using RedBolt.Bindings.Sdl;
using RedBolt.Engine;

internal static class WebProgram
{
    private static void Main()
    {
        var lz4Version = Lz4.Lz4VersionNumber();
        var sdlRevision = Encoding.UTF8.GetString(Sdl.SdlGetRevision());
        var diligentFactory = Diligent.LoadAndGetEngineFactoryOpenGL();
        var resourcesReady = ResourcesExist();
        // The browser frame loop continues after Run returns, so the page owns this application.
        var application = SampleCatalog.Create(BrowserDom.GetSelectedApplicationType());

        int exitCode;
        try
        {
            exitCode = application.Run([
                "--resource-paths", SampleCatalog.ResourcePaths,
                "--prefix-paths", "/",
            ]);
        }
        catch (Exception exception)
        {
            application.Dispose();
            var failureMessage = $"RedBolt WebAssembly sample failed with {exception.GetType().FullName}: {exception.Message}";
            Console.Error.WriteLine(exception);
            BrowserDom.SetResult(failureMessage, false);
            throw;
        }

        var success = exitCode == 0 && lz4Version > 0 && !string.IsNullOrEmpty(sdlRevision)
            && !diligentFactory.IsNull && resourcesReady;
        var message = exitCode == 0
            ? $"RedBolt WebAssembly engine initialized: LZ4 {lz4Version}, SDL {sdlRevision}, Diligent OpenGL factory {(!diligentFactory.IsNull ? "ready" : "missing")}, resources {(resourcesReady ? "ready" : "missing")}"
            : $"RedBolt WebAssembly engine failed: {application.ErrorMessage}";

        Console.WriteLine(message);
        BrowserDom.SetResult(message, success);
        if (!success)
            throw new InvalidOperationException(message);
    }

    private static bool ResourcesExist()
    {
        foreach (var resourcePath in SampleCatalog.ResourcePaths.Split(';'))
        {
            if (!File.Exists($"{resourcePath}.pak"))
                return false;
        }

        return true;
    }
}

internal static partial class BrowserDom
{
    [JSImport("dom.getSelectedApplicationType", "main.js")]
    internal static partial string GetSelectedApplicationType();

    [JSImport("dom.setResult", "main.js")]
    internal static partial void SetResult(string message, bool success);
}

internal static class SampleCatalog
{
    // <redbolt-generated name="web-resource-paths">
    internal const string ResourcePaths = "RedBolt.CoreData;RedBolt.SampleData;RedBolt.2D.CoreData;RedBolt.2D.SampleData;RedBolt.Glow.CoreData;RedBolt.Glow.SampleData;RedBolt.IK.SampleData;RedBolt.RmlUI.CoreData;RedBolt.RmlUI.SampleData";
    // </redbolt-generated name="web-resource-paths">

    internal static Application Create(string applicationTypeName) => applicationTypeName switch
    {
        // <redbolt-generated name="web-sample-factory">
        "RedBolt.Sample.Actions.ActionsApplication" => new global::RedBolt.Sample.Actions.ActionsApplication(),
        "RedBolt.Sample.AdvancedNetworking.AdvancedNetworking" => new global::RedBolt.Sample.AdvancedNetworking.AdvancedNetworking(),
        "RedBolt.Sample.AdvancedUI.AdvancedUIApplication" => new global::RedBolt.Sample.AdvancedUI.AdvancedUIApplication(),
        "RedBolt.Sample.AggregatedInput.AggregatedInputApplication" => new global::RedBolt.Sample.AggregatedInput.AggregatedInputApplication(),
        "RedBolt.Sample.AnimatingScene.AnimatingScene" => new global::RedBolt.Sample.AnimatingScene.AnimatingScene(),
        "RedBolt.Sample.BakedLighting.BakedLighting" => new global::RedBolt.Sample.BakedLighting.BakedLighting(),
        "RedBolt.Sample.Billboards.BillboardsApplication" => new global::RedBolt.Sample.Billboards.BillboardsApplication(),
        "RedBolt.Sample.CameraOperator.CameraOperatorSampleApplication" => new global::RedBolt.Sample.CameraOperator.CameraOperatorSampleApplication(),
        "RedBolt.Sample.CameraShake.CameraShakeApplication" => new global::RedBolt.Sample.CameraShake.CameraShakeApplication(),
        "RedBolt.Sample.CharacterDemo.CharacterDemo" => new global::RedBolt.Sample.CharacterDemo.CharacterDemo(),
        "RedBolt.Sample.Chat.Chat" => new global::RedBolt.Sample.Chat.Chat(),
        "RedBolt.Sample.CSG.CsgShowcase" => new global::RedBolt.Sample.CSG.CsgShowcase(),
        "RedBolt.Sample.CrowdNavigation.CrowdNavigationSample" => new global::RedBolt.Sample.CrowdNavigation.CrowdNavigationSample(),
        "RedBolt.Sample.Decals.DecalsApplication" => new global::RedBolt.Sample.Decals.DecalsApplication(),
        "RedBolt.Sample.DynamicGeometry.DynamicGeometry" => new global::RedBolt.Sample.DynamicGeometry.DynamicGeometry(),
        "RedBolt.Sample.HelloGUI.HelloGUI" => new global::RedBolt.Sample.HelloGUI.HelloGUI(),
        "RedBolt.Sample.HelloRmlUI.HelloRmlUI" => new global::RedBolt.Sample.HelloRmlUI.HelloRmlUI(),
        "RedBolt.Sample.HelloWorld.HelloWorld" => new global::RedBolt.Sample.HelloWorld.HelloWorld(),
        "RedBolt.Sample.HttpRequestDemo.HttpRequestDemo" => new global::RedBolt.Sample.HttpRequestDemo.HttpRequestDemo(),
        "RedBolt.Sample.HugeObjectCount.HugeObjectCountApplication" => new global::RedBolt.Sample.HugeObjectCount.HugeObjectCountApplication(),
        "RedBolt.Sample.InputLogger.InputLoggerApplication" => new global::RedBolt.Sample.InputLogger.InputLoggerApplication(),
        "RedBolt.Sample.InverseKinematics.InverseKinematicsApplication" => new global::RedBolt.Sample.InverseKinematics.InverseKinematicsApplication(),
        "RedBolt.Sample.KinematicCharacter.KinematicCharacterDemo" => new global::RedBolt.Sample.KinematicCharacter.KinematicCharacterDemo(),
        "RedBolt.Sample.LANDiscovery.LANDiscovery" => new global::RedBolt.Sample.LANDiscovery.LANDiscovery(),
        "RedBolt.Sample.LightAnimation.LightAnimationApplication" => new global::RedBolt.Sample.LightAnimation.LightAnimationApplication(),
        "RedBolt.Sample.MaterialAnimation.MaterialAnimationApplication" => new global::RedBolt.Sample.MaterialAnimation.MaterialAnimationApplication(),
        "RedBolt.Sample.MultipleViewports.MultipleViewports" => new global::RedBolt.Sample.MultipleViewports.MultipleViewports(),
        "RedBolt.Sample.Navigation.NavigationSample" => new global::RedBolt.Sample.Navigation.NavigationSample(),
        "RedBolt.Sample.Particles.ParticlesApplication" => new global::RedBolt.Sample.Particles.ParticlesApplication(),
        "RedBolt.Sample.Physics.PhysicsSample" => new global::RedBolt.Sample.Physics.PhysicsSample(),
        "RedBolt.Sample.Physics2D.Physics2D" => new global::RedBolt.Sample.Physics2D.Physics2D(),
        "RedBolt.Sample.Physics2DConstraints.Physics2DConstraints" => new global::RedBolt.Sample.Physics2DConstraints.Physics2DConstraints(),
        "RedBolt.Sample.Physics2DRope.Physics2DRope" => new global::RedBolt.Sample.Physics2DRope.Physics2DRope(),
        "RedBolt.Sample.PointerAdapter.PointerAdapterApplication" => new global::RedBolt.Sample.PointerAdapter.PointerAdapterApplication(),
        "RedBolt.Sample.RaycastVehicle.RaycastVehicleDemo" => new global::RedBolt.Sample.RaycastVehicle.RaycastVehicleDemo(),
        "RedBolt.Sample.RenderToTexture.RenderToTexture" => new global::RedBolt.Sample.RenderToTexture.RenderToTexture(),
        "RedBolt.Sample.SceneReplication.SceneReplication" => new global::RedBolt.Sample.SceneReplication.SceneReplication(),
        "RedBolt.Sample.SkeletalAnimation.SkeletalAnimationApplication" => new global::RedBolt.Sample.SkeletalAnimation.SkeletalAnimationApplication(),
        "RedBolt.Sample.SoundEffects.SoundEffects" => new global::RedBolt.Sample.SoundEffects.SoundEffects(),
        "RedBolt.Sample.SoundSynthesis.SoundSynthesis" => new global::RedBolt.Sample.SoundSynthesis.SoundSynthesis(),
        "RedBolt.Sample.StaticScene.StaticScene" => new global::RedBolt.Sample.StaticScene.StaticScene(),
        "RedBolt.Sample.TextureFormats.TextureFormatsSample" => new global::RedBolt.Sample.TextureFormats.TextureFormatsSample(),
        "RedBolt.Sample.Urho2DIsometricDemo.Urho2DIsometricDemo" => new global::RedBolt.Sample.Urho2DIsometricDemo.Urho2DIsometricDemo(),
        "RedBolt.Sample.Urho2DParticle.Urho2DParticle" => new global::RedBolt.Sample.Urho2DParticle.Urho2DParticle(),
        "RedBolt.Sample.Urho2DPlatformer.Urho2DPlatformer" => new global::RedBolt.Sample.Urho2DPlatformer.Urho2DPlatformer(),
        "RedBolt.Sample.Urho2DSprite.Urho2DSprite" => new global::RedBolt.Sample.Urho2DSprite.Urho2DSprite(),
        "RedBolt.Sample.Urho2DSpriterAnimation.Urho2DSpriterAnimation" => new global::RedBolt.Sample.Urho2DSpriterAnimation.Urho2DSpriterAnimation(),
        "RedBolt.Sample.Urho2DStretchableSprite.Urho2DStretchableSprite" => new global::RedBolt.Sample.Urho2DStretchableSprite.Urho2DStretchableSprite(),
        "RedBolt.Sample.Urho2DTileMap.Urho2DTileMap" => new global::RedBolt.Sample.Urho2DTileMap.Urho2DTileMap(),
        "RedBolt.Sample.VirtualFileSystem.VirtualFileSystemApplication" => new global::RedBolt.Sample.VirtualFileSystem.VirtualFileSystemApplication(),
        // </redbolt-generated name="web-sample-factory">
        _ => throw new InvalidOperationException($"Unknown RedBolt sample application: {applicationTypeName}"),
    };
}
