// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Sample.Android;

using System;
using System.Linq;

using global::Android.App;
using global::Android.Content;
using global::Android.Content.PM;
using global::Android.OS;
using global::Android.Widget;

using RedBolt.IO;

[Activity(Label = "RedBolt samples", MainLauncher = true, Exported = true)]
public sealed class SamplePickerActivity : Activity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        var requestedSample = Intent?.GetStringExtra(SampleActivity.SampleExtra);
        if (requestedSample is not null)
        {
            Launch(requestedSample);
            return;
        }

        var list = new ListView(this)
        {
            Adapter = new ArrayAdapter<string>(this, global::Android.Resource.Layout.SimpleListItem1,
                SampleCatalog.Entries.Select(static entry => entry.Name).ToArray())
        };
        list.ItemClick += (_, eventArgs) => Launch(SampleCatalog.Entries[eventArgs.Position].Id);
        SetContentView(list);
    }

    protected override void OnResume()
    {
        base.OnResume();
        if (Intent?.GetStringExtra(SampleActivity.SampleExtra) is not null)
            Finish();
    }

    private void Launch(string sampleId)
    {
        var intent = new Intent(this, typeof(SampleActivity));
        intent.PutExtra(SampleActivity.SampleExtra, sampleId);
        StartActivity(intent);
    }
}

[Activity(Label = "RedBolt samples", Exported = false,
    ScreenOrientation = ScreenOrientation.Landscape,
    ConfigurationChanges = ConfigChanges.KeyboardHidden | ConfigChanges.Orientation | ConfigChanges.ScreenSize)]
public sealed class SampleActivity : Org.Libsdl.App.SDLActivity
{
    internal const string SampleExtra = "sample";
    internal const string ScreenshotExtra = "screenshot";

    protected override void RunMain()
    {
        var sampleId = Intent?.GetStringExtra(SampleExtra)
            ?? throw new InvalidOperationException("A RedBolt sample id is required.");
        var sample = SampleCatalog.Entries.FirstOrDefault(entry => entry.Id == sampleId)
            ?? throw new InvalidOperationException($"Unknown RedBolt sample: {sampleId}");

        global::Android.Util.Log.Info("RedBolt", $"Starting sample {sample.Id}");
        VerifyPackagedAssets();
        using var application = sample.CreateApplication();
        try
        {
            var exitCode = application.Run([]);
            global::Android.Util.Log.Info("RedBolt", $"Sample {sample.Id} exited with code {exitCode}");
        }
        catch (Exception exception)
        {
            global::Android.Util.Log.Error("RedBolt", exception.ToString());
        }
    }

    private static void VerifyPackagedAssets()
    {
        using var assets = new MountedDirectory("/apk/CoreData");
        using var plane = assets.OpenFile(new FileIdentifier("Models/Plane.mdl"), RedBolt.IO.FileMode.Read)
            ?? throw new InvalidOperationException("Packaged CoreData assets are unavailable.");
        global::Android.Util.Log.Info("RedBolt", $"Verified packaged CoreData asset ({plane.Size} bytes)");
    }
}

internal sealed record SampleEntry(string Id, string Name, Func<RedBolt.Engine.Application> CreateApplication);

internal static class SampleCatalog
{
    internal static readonly SampleEntry[] Entries =
    [
        // <redbolt-generated name="android-sample-entries">
        new("actions:actions", "Actions", static () => new global::RedBolt.Sample.Actions.ActionsApplication()),
        new("physics-jolt:advanced-networking", "Advanced Networking", static () => new global::RedBolt.Sample.AdvancedNetworking.AdvancedNetworking()),
        new("rmlui:advanced-ui", "Advanced UI", static () => new global::RedBolt.Sample.AdvancedUI.AdvancedUIApplication()),
        new("redbolt:aggregated-input", "Aggregated Input", static () => new global::RedBolt.Sample.AggregatedInput.AggregatedInputApplication()),
        new("redbolt:animating-scene", "Animating Scene", static () => new global::RedBolt.Sample.AnimatingScene.AnimatingScene()),
        new("redbolt:billboards", "Billboards", static () => new global::RedBolt.Sample.Billboards.BillboardsApplication()),
        new("redbolt:camera-operator", "Camera Operator", static () => new global::RedBolt.Sample.CameraOperator.CameraOperatorSampleApplication()),
        new("redbolt:camera-shake", "Camera Shake", static () => new global::RedBolt.Sample.CameraShake.CameraShakeApplication()),
        new("physics-jolt:character-demo", "Character Demo", static () => new global::RedBolt.Sample.CharacterDemo.CharacterDemo()),
        new("network:chat", "Chat", static () => new global::RedBolt.Sample.Chat.Chat()),
        new("csg:csg", "Constructive Solid Geometry", static () => new global::RedBolt.Sample.CSG.CsgShowcase()),
        new("navigation:crowd-navigation", "Crowd Navigation", static () => new global::RedBolt.Sample.CrowdNavigation.CrowdNavigationSample()),
        new("redbolt:decals", "Decals", static () => new global::RedBolt.Sample.Decals.DecalsApplication()),
        new("redbolt:dynamic-geometry", "Dynamic Geometry", static () => new global::RedBolt.Sample.DynamicGeometry.DynamicGeometry()),
        new("redbolt:hello-gui", "Hello GUI", static () => new global::RedBolt.Sample.HelloGUI.HelloGUI()),
        new("rmlui:hello-rmlui", "Hello RmlUI", static () => new global::RedBolt.Sample.HelloRmlUI.HelloRmlUI()),
        new("redbolt:hello-world", "Hello World", static () => new global::RedBolt.Sample.HelloWorld.HelloWorld()),
        new("network:http-request", "HTTP Request", static () => new global::RedBolt.Sample.HttpRequestDemo.HttpRequestDemo()),
        new("redbolt:huge-object-count", "Huge Object Count", static () => new global::RedBolt.Sample.HugeObjectCount.HugeObjectCountApplication()),
        new("redbolt:input-logger", "Input Logger", static () => new global::RedBolt.Sample.InputLogger.InputLoggerApplication()),
        new("ik:inverse-kinematics", "Inverse Kinematics", static () => new global::RedBolt.Sample.InverseKinematics.InverseKinematicsApplication()),
        new("physics-jolt:kinematic-character", "Kinematic Character", static () => new global::RedBolt.Sample.KinematicCharacter.KinematicCharacterDemo()),
        new("network:lan-discovery", "LAN Discovery", static () => new global::RedBolt.Sample.LANDiscovery.LANDiscovery()),
        new("redbolt:light-animation", "Light Animation", static () => new global::RedBolt.Sample.LightAnimation.LightAnimationApplication()),
        new("redbolt:material-animation", "Material Animation", static () => new global::RedBolt.Sample.MaterialAnimation.MaterialAnimationApplication()),
        new("redbolt:multiple-viewports", "Multiple Viewports", static () => new global::RedBolt.Sample.MultipleViewports.MultipleViewports()),
        new("navigation:navigation", "Navigation", static () => new global::RedBolt.Sample.Navigation.NavigationSample()),
        new("particles:particles", "Particles", static () => new global::RedBolt.Sample.Particles.ParticlesApplication()),
        new("physics-jolt:physics", "Physics", static () => new global::RedBolt.Sample.Physics.PhysicsSample()),
        new("physics-box2d:physics2d", "Physics2D", static () => new global::RedBolt.Sample.Physics2D.Physics2D()),
        new("physics-box2d:physics2d-constraints", "Physics2D Constraints", static () => new global::RedBolt.Sample.Physics2DConstraints.Physics2DConstraints()),
        new("physics-box2d:physics2d-rope", "Physics2D Rope", static () => new global::RedBolt.Sample.Physics2DRope.Physics2DRope()),
        new("redbolt:pointer-adapter", "Pointer Adapter", static () => new global::RedBolt.Sample.PointerAdapter.PointerAdapterApplication()),
        new("physics-jolt:raycast-vehicle", "Raycast Vehicle", static () => new global::RedBolt.Sample.RaycastVehicle.RaycastVehicleDemo()),
        new("redbolt:render-to-texture", "Render to Texture", static () => new global::RedBolt.Sample.RenderToTexture.RenderToTexture()),
        new("physics-jolt:scene-replication", "Scene Replication", static () => new global::RedBolt.Sample.SceneReplication.SceneReplication()),
        new("redbolt:skeletal-animation", "Skeletal Animation", static () => new global::RedBolt.Sample.SkeletalAnimation.SkeletalAnimationApplication()),
        new("redbolt:sound-effects", "Sound Effects", static () => new global::RedBolt.Sample.SoundEffects.SoundEffects()),
        new("redbolt:sound-synthesis", "Sound Synthesis", static () => new global::RedBolt.Sample.SoundSynthesis.SoundSynthesis()),
        new("redbolt:static-scene", "Static Scene", static () => new global::RedBolt.Sample.StaticScene.StaticScene()),
        new("redbolt:texture-formats", "Texture Formats", static () => new global::RedBolt.Sample.TextureFormats.TextureFormatsSample()),
        new("physics-box2d:urho2d-isometric-demo", "Urho2D Isometric Demo", static () => new global::RedBolt.Sample.Urho2DIsometricDemo.Urho2DIsometricDemo()),
        new("2d:urho2d-particle", "Urho2D Particle", static () => new global::RedBolt.Sample.Urho2DParticle.Urho2DParticle()),
        new("physics-box2d:urho2d-platformer", "Urho2D Platformer", static () => new global::RedBolt.Sample.Urho2DPlatformer.Urho2DPlatformer()),
        new("2d:urho2d-sprite", "Urho2D Sprite", static () => new global::RedBolt.Sample.Urho2DSprite.Urho2DSprite()),
        new("2d:urho2d-spriter-animation", "Urho2D Spriter Animation", static () => new global::RedBolt.Sample.Urho2DSpriterAnimation.Urho2DSpriterAnimation()),
        new("2d:urho2d-stretchable-sprite", "Urho2D Stretchable Sprite", static () => new global::RedBolt.Sample.Urho2DStretchableSprite.Urho2DStretchableSprite()),
        new("2d:urho2d-tile-map", "Urho2D Tile Map", static () => new global::RedBolt.Sample.Urho2DTileMap.Urho2DTileMap()),
        new("redbolt:virtual-file-system", "Virtual File System", static () => new global::RedBolt.Sample.VirtualFileSystem.VirtualFileSystemApplication()),
        // </redbolt-generated name="android-sample-entries">
    ];
}
