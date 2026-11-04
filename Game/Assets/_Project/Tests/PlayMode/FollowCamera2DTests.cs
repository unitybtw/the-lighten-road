using System.Collections;
using NUnit.Framework;
using TheLightenRoad.Player;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class FollowCamera2DTests
{
    private GameObject cameraObject;
    private GameObject targetObject;
    private FollowCamera2D cameraFollow;
    private Rigidbody2D targetBody;
    private int reducedMotion;
    private bool hadReducedMotion;
    private float previousTimeScale;

    [SetUp]
    public void SetUp()
    {
        hadReducedMotion = PlayerPrefs.HasKey("TLR.Menu.ReducedMotion");
        previousTimeScale = Time.timeScale;
        Time.timeScale = 1f;
        reducedMotion = PlayerPrefs.GetInt("TLR.Menu.ReducedMotion", 0);
        PlayerPrefs.SetInt("TLR.Menu.ReducedMotion", 0);
        targetObject = new GameObject("Test Target");
        targetObject.transform.position = Vector3.zero;
        targetBody = targetObject.AddComponent<Rigidbody2D>();
        targetBody.gravityScale = 0f;

        cameraObject = new GameObject("Test Camera");
        var cam = cameraObject.AddComponent<Camera>();
        cam.enabled = false;
        cameraFollow = cameraObject.AddComponent<FollowCamera2D>();
        cameraFollow.SetTarget(targetObject.transform, snap: true);
    }

    [TearDown]
    public void TearDown()
    {
        if (hadReducedMotion) PlayerPrefs.SetInt("TLR.Menu.ReducedMotion", reducedMotion);
        else PlayerPrefs.DeleteKey("TLR.Menu.ReducedMotion");
        Time.timeScale = previousTimeScale;
        if (cameraObject != null)
            Object.Destroy(cameraObject);
        if (targetObject != null)
            Object.Destroy(targetObject);
    }

    [UnityTest]
    public IEnumerator FollowCameraSnapsAndTracksTargetWithOffset()
    {
        cameraFollow.Offset = new Vector3(0f, 2f, -10f);
        cameraFollow.SetTarget(targetObject.transform, snap: true);

        Assert.That(cameraObject.transform.position.x, Is.EqualTo(0f).Within(0.01f));
        Assert.That(cameraObject.transform.position.y, Is.EqualTo(2f).Within(0.01f));
        Assert.That(cameraObject.transform.position.z, Is.EqualTo(-10f).Within(0.01f));

        targetObject.transform.position = new Vector3(5f, 4f, 0f);
        yield return new WaitForSeconds(0.4f);

        Assert.That(cameraObject.transform.position.x, Is.GreaterThan(3f));
        Assert.That(cameraObject.transform.position.y, Is.GreaterThan(4f));
    }

    [UnityTest]
    public IEnumerator CameraLeadsAheadWhenTargetMovesHorizontally()
    {
        cameraFollow.EnableLookAhead = true;
        cameraFollow.LookAheadDistance = 1.0f;
        targetBody.linearVelocity = new Vector2(5f, 0f);

        yield return new WaitForSeconds(0.3f);
        Assert.That(cameraFollow.CurrentLookAheadX, Is.GreaterThan(0.5f),
            "Moving right should lead the camera on the positive X axis.");

        targetBody.linearVelocity = new Vector2(-5f, 0f);
        yield return new WaitForSeconds(0.4f);
        Assert.That(cameraFollow.CurrentLookAheadX, Is.LessThan(-0.5f),
            "Moving left should lead the camera on the negative X axis.");
    }

    [UnityTest]
    public IEnumerator CameraAnticipatesFallWhenDescendingRapidly()
    {
        cameraFollow.EnableFallAnticipation = true;
        cameraFollow.FallVelocityThreshold = -3f;
        cameraFollow.FallLeadDistance = 1.5f;

        targetBody.linearVelocity = new Vector2(0f, -8f);
        yield return new WaitForSeconds(0.3f);

        Assert.That(cameraFollow.CurrentFallLeadY, Is.LessThan(-0.5f),
            "Falling downward rapidly should shift the camera target downwards.");
    }

    [UnityTest]
    public IEnumerator CameraShakeActivatesAndDecays()
    {
        Assert.That(cameraFollow.IsShaking, Is.False);

        cameraFollow.Shake(0.2f, 0.15f);
        Assert.That(cameraFollow.IsShaking, Is.True);

        yield return new WaitForSeconds(0.05f);
        Assert.That(cameraFollow.IsShaking, Is.True);

        yield return new WaitForSeconds(0.15f);
        Assert.That(cameraFollow.IsShaking, Is.False, "Shake should expire after duration.");
    }

    [UnityTest]
    public IEnumerator ReducedMotionStopsAnExistingShakeAndRejectsNewOnes()
    {
        cameraFollow.Shake(.2f, 1f);
        Assert.That(cameraFollow.IsShaking, Is.True);
        Time.timeScale = 0f;
        PlayerPrefs.SetInt("TLR.Menu.ReducedMotion", 1);
        yield return null;
        yield return null;
        Assert.That(cameraFollow.IsShaking, Is.False);
        Assert.That(cameraObject.transform.position, Is.EqualTo(cameraFollow.CurrentFollowPosition));
        cameraFollow.Shake(.2f, 1f);
        Assert.That(cameraFollow.IsShaking, Is.False);
    }

    [UnityTest]
    public IEnumerator TeleportSnapsCameraAndClearsOldMotion()
    {
        yield return null;
        cameraFollow.Shake(.2f, 1f);
        targetObject.transform.position = new Vector3(20f, 3f, 0f);
        yield return null;
        yield return null;
        Assert.That(cameraFollow.CurrentFollowPosition.x, Is.EqualTo(20f).Within(.01f));
        Assert.That(cameraFollow.CurrentFollowPosition.y, Is.EqualTo(4.5f).Within(.05f));
        Assert.That(cameraFollow.IsShaking, Is.False);
    }

    [UnityTest]
    public IEnumerator SmallVerticalMovementKeepsFrameSteadyButLargerClimbIsFollowed()
    {
        yield return null;
        float initialY = cameraFollow.CurrentFollowPosition.y;
        targetObject.transform.position += Vector3.up * .1f;
        yield return new WaitForSeconds(.25f);
        Assert.That(cameraFollow.CurrentFollowPosition.y, Is.EqualTo(initialY).Within(.02f));
        targetObject.transform.position += Vector3.up * 2f;
        yield return new WaitForSeconds(.4f);
        Assert.That(cameraFollow.CurrentFollowPosition.y, Is.GreaterThan(initialY + 1.4f));
    }
    private void AssertViewportInside(Rect area)
    {
        var view = cameraObject.GetComponent<Camera>();
        foreach (var uv in new[] { Vector2.zero, Vector2.one, Vector2.up, Vector2.right })
        {
            var point = view.ViewportToWorldPoint(new Vector3(uv.x, uv.y, 10));
            Assert.That(point.x, Is.InRange(area.xMin - .001f, area.xMax + .001f));
            Assert.That(point.y, Is.InRange(area.yMin - .001f, area.yMax + .001f));
        }
    }
    [UnityTest] public IEnumerator WorldEdgesContainTheWholeViewportAfterSnapsAndEdgeShakes()
    {
        var view = cameraObject.GetComponent<Camera>(); view.orthographic = true; view.aspect = 16f / 9f;
        var area = new Rect(-10, -5, 20, 10); cameraFollow.SetWorldBounds(area, 3);
        foreach (var position in new[] { new Vector2(-30,-30),new Vector2(30,30),new Vector2(-30,30),new Vector2(30,-30) })
        {
            targetObject.transform.position = position;
            cameraFollow.SetTarget(targetObject.transform, true); AssertViewportInside(area);
            cameraFollow.Shake(.8f,.2f); yield return new WaitForSeconds(.08f); AssertViewportInside(area);
        }
    }
    [UnityTest] public IEnumerator OversizedViewFitsTheRoomAndPausedAspectChangesRemainContained()
    {
        var view = cameraObject.GetComponent<Camera>(); view.orthographic = true; view.aspect = 32f / 9f;
        var area = new Rect(-4,-2,8,4);cameraFollow.SetWorldBounds(area,3);
        cameraFollow.SetTarget(targetObject.transform,true);AssertViewportInside(area);
        Assert.That(view.orthographicSize, Is.EqualTo(1.125f).Within(.001f));
        Time.timeScale = 0; view.aspect = 16f / 9f; yield return null; yield return null;
        AssertViewportInside(area); Assert.That(view.orthographicSize, Is.EqualTo(2).Within(.001f));
    }

}
