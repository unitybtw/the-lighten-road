using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TheLightenRoad.Lantern;
using TheLightenRoad.Player;
using TheLightenRoad.Player.Completion;
using TheLightenRoad.Sirac.FurkanPrototype;
using TheLightenRoad.Input;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class ChamberCompletionTests
{
    private Scene scene;
    private FurkanLevelDirector level;
    private ChamberCompletionSequence sequence;
    private ChamberCompletionProfile profile;
    private float previousTimeScale;
    [UnitySetUp] public IEnumerator Setup()
    {
        Time.timeScale=1f;
        previousTimeScale=Time.timeScale;
#if UNITY_EDITOR
        scene=UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode("Assets/_Project/Scenes/Sandbox/Sirac/FurkanTutorial.unity",new LoadSceneParameters(LoadSceneMode.Additive));
        while (!scene.isLoaded) yield return null;
        SceneManager.SetActiveScene(scene);
#endif
        level=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<FurkanLevelDirector>()).Single();
        sequence=level.Completion;
        Assert.That(sequence,Is.Not.Null,"Tutorial must have a wired reusable exit.");
        profile=Object.Instantiate(sequence.Profile);sequence.Profile=profile;
        profile.ExtinguishSeconds=.12f;profile.RevealSeconds=.12f;profile.HoldSeconds=.1f;profile.FadeOutSeconds=.12f;profile.FadeInSeconds=.12f;
        sequence.DestinationScene="";
    }
    [UnityTearDown] public IEnumerator Teardown()
    {
        Time.timeScale=1f;
        if(sequence!=null) { sequence.Cancel(); Object.Destroy(sequence.gameObject); }
        if(profile!=null)Object.Destroy(profile);
        if(scene.IsValid() && scene.isLoaded)
        {
            if (SceneManager.GetActiveScene() == scene)
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var s = SceneManager.GetSceneAt(i);
                    if (s != scene && s.isLoaded)
                    {
                        SceneManager.SetActiveScene(s);
                        break;
                    }
                }
            }
            var op = SceneManager.UnloadSceneAsync(scene);
            while (op != null && !op.isDone) yield return null;
        }
        // Single travel replaces the runner's empty scene. Restore an empty scene, not a real
        // frontend whose cameras/lights/geometry would overlap later additive gameplay fixtures.
        if(SceneManager.GetActiveScene().name=="ArchiveTestChamber")
        {
            var archive=SceneManager.GetActiveScene();
            var empty=SceneManager.CreateScene("Completion tests / empty host");
            SceneManager.SetActiveScene(empty);
            var op2 = SceneManager.UnloadSceneAsync(archive);
            while (op2 != null && !op2.isDone) yield return null;
        }
        yield return null;
    }
    [UnityTest] public IEnumerator CompletionRunsOnceWithOrderedEventsAndRestoresControlAtZeroTimeScale()
    {
        int started=0,covered=0,finished=0;
        sequence.Started.AddListener(()=>started++);
        sequence.ScreenCovered.AddListener(()=>{Assert.That(sequence.CurtainAlpha,Is.EqualTo(1f));covered++;});
        sequence.Finished.AddListener(()=>{Assert.That(covered,Is.EqualTo(1));finished++;});
        var motor=level.Player.GetComponent<PlayerMotor2D>();
        int embers=level.Player.GetComponent<LanternFuel>().TotalEmbers;
        Time.timeScale=0;
        Assert.That(sequence.TryBegin(level.Player),Is.True);
        Assert.That(sequence.TryBegin(level.Player),Is.False);
        Assert.That(motor.enabled,Is.False);Assert.That(motor.Body.simulated,Is.False);
        yield return new WaitForSecondsRealtime(.9f);
        Assert.That(sequence.HasCompleted,Is.True);Assert.That(sequence.IsRunning,Is.False);
        Assert.That(started,Is.EqualTo(1));Assert.That(covered,Is.EqualTo(1));Assert.That(finished,Is.EqualTo(1));
        Assert.That(motor.enabled,Is.True);Assert.That(motor.Body.simulated,Is.True);
        Assert.That(level.Player.enabled,Is.True);
        Assert.That(level.Player.GetComponent<LanternFuel>().TotalEmbers,Is.EqualTo(embers));
        Assert.That(Time.timeScale,Is.Zero);
        Time.timeScale=1f;
        Assert.That(sequence.TryBegin(level.Player),Is.False);
    }
    [UnityTest] public IEnumerator CancelRestoresPreviouslyDisabledComponentsAndAllowsRetry()
    {
        var lantern=level.Player.GetComponent<LanternController>();lantern.enabled=false;
        profile.HoldSeconds=3;
        Assert.That(sequence.TryBegin(level.Player),Is.True);
        yield return new WaitForSecondsRealtime(.2f);
        Assert.That(lantern.PresentationVisibility,Is.Zero);
        Assert.That(lantern.LanternLight.intensity,Is.Zero);
        Assert.That(sequence.Cancel(),Is.True);
        Assert.That(lantern.enabled,Is.False);
        Assert.That(lantern.PresentationVisibility,Is.EqualTo(1f));
        Assert.That(level.Player.GetComponent<PlayerMotor2D>().enabled,Is.True);
        Assert.That(level.Player.GetComponent<Rigidbody2D>().simulated,Is.True);
        Assert.That(sequence.CurtainAlpha,Is.Zero);
        level.ResetAttempt();
        Assert.That(sequence.TryBegin(level.Player),Is.True);
    }
    [UnityTest] public IEnumerator InvalidDestinationDoesNotLockPlayerOrStartPresentation()
    {
        sequence.DestinationScene="Assets/Nonexistent.unity";
        LogAssert.Expect(LogType.Warning,"[ChamberCompletion] Destination is not enabled in Build Settings: Assets/Nonexistent.unity");
        Assert.That(sequence.TryBegin(level.Player),Is.False);
        Assert.That(sequence.IsRunning,Is.False);
        Assert.That(level.Player.GetComponent<PlayerMotor2D>().enabled,Is.True);
        Assert.That(level.Player.GetComponent<Rigidbody2D>().simulated,Is.True);
        yield return null;
    }
    [UnityTest] public IEnumerator LocalDestinationResetsChamberUnderCoverAndDisableInterruptsSafely()
    {
        var target=new GameObject("Test checkpoint");SceneManager.MoveGameObjectToScene(target,scene);
        target.transform.position=new Vector3(38,3.3f);sequence.DestinationSpawn=target.transform;
        level.Player.GetComponent<LanternFuel>().TrySpendEmber();
        sequence.ScreenCovered.AddListener(()=>Assert.That(sequence.CurtainAlpha,Is.EqualTo(1)));
        Assert.That(sequence.TryBegin(level.Player),Is.True);
        yield return new WaitForSecondsRealtime(.9f);
        Assert.That(level.Player.SpawnPosition,Is.EqualTo(target.transform.position));
        Assert.That(level.Player.GetComponent<LanternFuel>().TotalEmbers,Is.EqualTo(3));
        sequence.ResetCompletion();profile.HoldSeconds=5;
        Assert.That(sequence.TryBegin(level.Player),Is.True);
        sequence.enabled=false;yield return null;
        Assert.That(level.Player.GetComponent<PlayerMotor2D>().enabled,Is.True);
        Assert.That(level.Player.enabled,Is.True);
        Assert.That(sequence.IsRunning,Is.False);
    }
    [UnityTest] public IEnumerator AuthoredExitTransfersToArchiveAndRemovesPersistentPresentation()
    {
        sequence.DestinationScene="Assets/_Project/Scenes/Sandbox/Sirac/ArchiveTestChamber.unity";
        sequence.Finished.AddListener(() => throw new System.InvalidOperationException("finish listener failed"));
        LogAssert.Expect(LogType.Exception, new Regex("finish listener failed"));
        var body=level.Player.GetComponent<Rigidbody2D>();
        level.Player.transform.position=new Vector3(40.5f,3.25f);
        body.position=new Vector2(40.5f,3.25f);
        Physics2D.SyncTransforms();
        body.WakeUp();
        yield return new WaitForFixedUpdate();
        if(!sequence.IsRunning) sequence.TryBegin(level.Player);
        float timeout=Time.realtimeSinceStartup+12;
        while(SceneManager.GetActiveScene().name!="ArchiveTestChamber" && Time.realtimeSinceStartup<timeout)yield return null;
        Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("ArchiveTestChamber"));
        yield return new WaitForSecondsRealtime(.5f);
        Assert.That(Object.FindAnyObjectByType<ChamberCompletionSequence>(),Is.Null);
        var player=Object.FindAnyObjectByType<PlayerRespawnController>();
        Assert.That(player.enabled,Is.True);Assert.That(player.GetComponent<PlayerMotor2D>().enabled,Is.True);
        Assert.That(player.GetComponent<Rigidbody2D>().simulated,Is.True);
        Assert.That(player.GetComponent<LanternFuel>().TotalEmbers,Is.EqualTo(3));
        Assert.That(GameObject.Find("Completion / presentation"),Is.Null);
    }

    [UnityTest] public IEnumerator CompletionOwnsInputAndLegacyPollingWithoutReleasingAnotherOwner()
    {
        var type=System.AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("TheLightenRoad.Interaction.EmberVein")).First(t=>t!=null);
        var node=new GameObject("Completion legacy input regression");SceneManager.MoveGameObjectToScene(node,scene);
        var circuit=(Behaviour)node.AddComponent(type);
        profile.HoldSeconds=3;
        Assert.That(sequence.TryBegin(level.Player),Is.True);
        Assert.That(GameControls.GameplayAllowed,Is.False);
        Assert.That(circuit.enabled,Is.False,"Raw E polling must not transfer embers behind the exit presentation.");
        GameControls.BlockGameplay(profile,true);LegacyInteractionPollingGate.Acquire(profile);
        try
        {
            sequence.Cancel();yield return null;yield return null;yield return null;
            Assert.That(GameControls.GameplayAllowed,Is.False);
            Assert.That(circuit.enabled,Is.False);
        }
        finally {GameControls.BlockGameplay(profile,false);LegacyInteractionPollingGate.Release(profile);}
        yield return null;yield return null;yield return null;
        Assert.That(GameControls.GameplayAllowed,Is.True);Assert.That(circuit.enabled,Is.True);
    }

    [UnityTest] public IEnumerator ClimbingPlayerCannotBeTakenOverByAnExitPresentation()
    {
        var motor=level.Player.GetComponent<PlayerMotor2D>();motor.SetTraversalLocked(true);
        try
        {
            Assert.That(sequence.TryBegin(level.Player),Is.False);
            Assert.That(motor.enabled,Is.True);Assert.That(motor.Body.simulated,Is.True);
        }
        finally {motor.SetTraversalLocked(false);}
        yield return null;
    }

    [UnityTest] public IEnumerator AnotherInputOwnerPreventsStartingAnExitBehindAModal()
    {
        GameControls.BlockGameplay(profile,true);
        try
        {
            Assert.That(sequence.TryBegin(level.Player),Is.False);
            Assert.That(level.Player.enabled,Is.True);
            Assert.That(sequence.IsRunning,Is.False);
        }
        finally {GameControls.BlockGameplay(profile,false);}
        yield return null;yield return null;
    }

    [UnityTest] public IEnumerator FailingCompletionListenersDoNotStrandThePlayer()
    {
        sequence.Started.AddListener(() => throw new System.InvalidOperationException("start listener failed"));
        sequence.ScreenCovered.AddListener(() => throw new System.InvalidOperationException("cover listener failed"));
        LogAssert.Expect(LogType.Exception, new Regex("start listener failed"));
        LogAssert.Expect(LogType.Exception, new Regex("cover listener failed"));
        Assert.That(sequence.TryBegin(level.Player), Is.True);
        yield return new WaitForSecondsRealtime(1f);
        Assert.That(sequence.HasCompleted, Is.True);
        Assert.That(sequence.IsRunning, Is.False);
        Assert.That(level.Player.enabled, Is.True);
        Assert.That(level.Player.GetComponent<PlayerMotor2D>().enabled, Is.True);
        Assert.That(level.Player.GetComponent<Rigidbody2D>().simulated, Is.True);
        Assert.That(GameControls.GameplayAllowed, Is.True);
    }

    [UnityTest] public IEnumerator CancelFromStartedListenerEndsCleanlyAndAllowsRetry()
    {
        UnityEngine.Events.UnityAction cancel = () => sequence.Cancel();
        sequence.Started.AddListener(cancel);
        Assert.That(sequence.TryBegin(level.Player), Is.True);
        yield return null;
        Assert.That(sequence.IsRunning, Is.False);
        Assert.That(level.Player.enabled, Is.True);
        Assert.That(level.Player.GetComponent<PlayerMotor2D>().enabled, Is.True);
        Assert.That(sequence.CurtainAlpha, Is.Zero);
        sequence.Started.RemoveListener(cancel);
        yield return null; yield return null;
        Assert.That(sequence.TryBegin(level.Player), Is.True);
    }

    [UnityTest] public IEnumerator LanternGoesDarkBeforeTheTitleAndRelightsBeforeControlReturns()
    {
        var lantern=level.Player.GetComponent<LanternController>();
        lantern.EnableFlicker=false;
        int embers=lantern.Fuel.TotalEmbers;
        profile.ExtinguishSeconds=.35f;profile.RevealSeconds=.2f;
        profile.HoldSeconds=.2f;profile.FadeOutSeconds=.15f;profile.FadeInSeconds=.5f;
        Assert.That(sequence.TryBegin(level.Player),Is.True);
        Assert.That(sequence.TitleAlpha,Is.Zero);
        yield return new WaitForSecondsRealtime(.12f);
        Assert.That(lantern.PresentationVisibility,Is.InRange(.01f,.99f));
        Assert.That(lantern.LanternLight.intensity,Is.InRange(.001f,lantern.DimIntensity-.001f));
        Assert.That(sequence.TitleAlpha,Is.Zero,"Text must wait for the lantern to go out.");
        yield return WaitForPhase("Hold");
        Assert.That(lantern.LanternLight.intensity,Is.Zero);
        Assert.That(sequence.CurtainAlpha,Is.EqualTo(1f));
        Assert.That(sequence.TitleAlpha,Is.EqualTo(1f));
        Assert.That(GameObject.Find("Warm light / arrival"),Is.Null,"The player's lantern owns the light; no extra exit light.");
        yield return WaitForPhase("FadeIn");
        yield return new WaitForSecondsRealtime(.15f);
        Assert.That(sequence.TitleAlpha,Is.Zero);
        Assert.That(lantern.PresentationVisibility,Is.InRange(.01f,.99f));
        Assert.That(sequence.CurtainAlpha,Is.LessThan(1f));
        Assert.That(level.Player.GetComponent<PlayerMotor2D>().enabled,Is.False);
        yield return WaitForPhase("Complete");
        Assert.That(lantern.PresentationVisibility,Is.EqualTo(1f));
        Assert.That(lantern.LanternLight.intensity,Is.EqualTo(lantern.DimIntensity).Within(.001f));
        Assert.That(lantern.Fuel.TotalEmbers,Is.EqualTo(embers));
    }

    [UnityTest] public IEnumerator CancellingAnExtinguishRestoresAnActiveScoutWithoutAdvancingItsTimer()
    {
        var lantern=level.Player.GetComponent<LanternController>();
        lantern.SetFlared(true);lantern.TickScout(lantern.TransitionDuration);
        lantern.SetPresentationVisibility(.7f);
        float timer=lantern.FlareTimer;
        int embers=lantern.Fuel.TotalEmbers;
        profile.ExtinguishSeconds=1f;
        Assert.That(sequence.TryBegin(level.Player),Is.True);
        Assert.That(lantern.PresentationVisibility,Is.EqualTo(.7f),"The initiating frame is not elapsed transition time.");
        yield return new WaitForSecondsRealtime(.2f);
        Assert.That(lantern.PresentationVisibility,Is.LessThan(.7f));
        Assert.That(sequence.Cancel(),Is.True);
        Assert.That(lantern.PresentationVisibility,Is.EqualTo(.7f));
        Assert.That(lantern.FlareTimer,Is.EqualTo(timer));
        Assert.That(lantern.HasActiveScout,Is.True);
        Assert.That(lantern.Fuel.TotalEmbers,Is.EqualTo(embers));
    }

    private IEnumerator WaitForPhase(string phase)
    {
        float deadline=Time.realtimeSinceStartup+4f;
        while(sequence.Phase!=phase && Time.realtimeSinceStartup<deadline)yield return null;
        Assert.That(sequence.Phase,Is.EqualTo(phase));
    }
}
