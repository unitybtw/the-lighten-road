using System.Collections;
using System.Linq;
using NUnit.Framework;
using TheLightenRoad.Lantern;
using TheLightenRoad.Player;
using TheLightenRoad.Player.Completion;
using TheLightenRoad.Sirac.FurkanPrototype;
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
        previousTimeScale=Time.timeScale;
#if UNITY_EDITOR
        scene=UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode("Assets/_Project/Scenes/Sandbox/Sirac/FurkanTutorial.unity",new LoadSceneParameters(LoadSceneMode.Additive));
#endif
        yield return null;
        level=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<FurkanLevelDirector>()).Single();
        sequence=level.Completion;
        Assert.That(sequence,Is.Not.Null,"Tutorial must have a wired reusable exit.");
        profile=Object.Instantiate(sequence.Profile);sequence.Profile=profile;
        profile.RevealSeconds=.12f;profile.HoldSeconds=.1f;profile.FadeOutSeconds=.12f;profile.FadeInSeconds=.12f;
        sequence.DestinationScene="";
    }
    [UnityTearDown] public IEnumerator Teardown()
    {
        Time.timeScale=previousTimeScale;
        if(sequence!=null) { sequence.Cancel(); Object.Destroy(sequence.gameObject); }
        if(profile!=null)Object.Destroy(profile);
        if(scene.IsValid() && scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);
        // Single travel replaces the runner's empty scene. Restore an empty scene, not a real
        // frontend whose cameras/lights/geometry would overlap later additive gameplay fixtures.
        if(SceneManager.GetActiveScene().name=="ArchiveTestChamber")
        {
            var archive=SceneManager.GetActiveScene();
            var empty=SceneManager.CreateScene("Completion tests / empty host");
            SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync(archive);
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
        Assert.That(sequence.TryBegin(level.Player),Is.False);
    }
    [UnityTest] public IEnumerator CancelRestoresPreviouslyDisabledComponentsAndAllowsRetry()
    {
        var lantern=level.Player.GetComponent<LanternController>();lantern.enabled=false;
        profile.HoldSeconds=3;
        Assert.That(sequence.TryBegin(level.Player),Is.True);
        yield return new WaitForSecondsRealtime(.2f);
        Assert.That(sequence.LightRadius,Is.GreaterThan(1f));
        Assert.That(sequence.Cancel(),Is.True);
        Assert.That(lantern.enabled,Is.False);
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
        var body=level.Player.GetComponent<Rigidbody2D>();
        body.position=new Vector2(40.5f,3.25f);
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
}
