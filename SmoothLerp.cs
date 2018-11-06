using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// Class is undone, got to implement custom editor

public class SmoothLerp : MonoBehaviour
{
    public AnimationCurve Curve;
    public LerpingObject LerpingType;
    public float Speed = 1f;
    public bool LerpOnAwake = false;
    public bool LerpBackOnSleep = false;
    public bool DisableGameobjectOnEnd = false;
    public bool ApplyLerpForChildren = false;  /// TODO: make for non-transform only in custom editor

    /// <summary>
    /// Returns true, if SmoothLerp is currently interpolating
    /// Tip: Use it, if you want to wait until interpolation is finished
    /// </summary>
    [HideInInspector] public bool nowLerping = false;
    [HideInInspector] public bool _nowLerpingBack = false;

    [Header("Start-End states")]                    /// TODO: Custom Editor
    public Vector3 StartPosition, EndPosition;
    public Vector3 StartScale, EndScale;
    public float StartCamSize, EndCamSize;
    public Color StartColor, EndColor;

    private Renderer[] _renderers;
    public Image[] _images;
    public Text[] _texts;

    private Coroutine _coroutine;


    private void Awake()
    {
        if (ApplyLerpForChildren)
        {
            _renderers = GetComponentsInChildren<Renderer>();
            _images = GetComponentsInChildren<Image>();
            _texts = GetComponentsInChildren<Text>();
        }
        else
            _renderers = new Renderer[] { GetComponent<Renderer>() };
    }

    private void OnEnable()
    {
        if (LerpOnAwake) Lerp();
    }

    // Uses preset values
    public void Lerp()
    {
        if (_coroutine != null)
            StopCoroutine(_coroutine);
        nowLerping = true;
        _coroutine = StartCoroutine(_lerp());
    }

    // Position or Scale lerp
    public void Lerp(Vector3 start, Vector3 end, float speed = 1f, bool isTransformLerp = true, AnimationCurve curve = null)
    {
        if (isTransformLerp)
        {
            LerpingType = LerpingObject.Transform;
            StartPosition = start;
            EndPosition = end;
        }
        else
        {
            LerpingType = LerpingObject.Scale;
            StartScale = start;
            EndScale = end;
        }
        Speed = speed;
        if (curve != null) Curve = curve;

        Lerp();
    }

    // Color lerp
    public void Lerp(Color start, Color end, float speed = 1f, AnimationCurve curve = null)
    {
        LerpingType = LerpingObject.Color;
        StartColor = start;
        EndColor = end;
        Speed = speed;
        if (curve != null) Curve = curve;

        Lerp();
    }

    private IEnumerator _lerp()
    {
        var lastFrameTime = Curve[Curve.length - 1].time; // last keyframe's time

        var curveTime =    _nowLerpingBack ? lastFrameTime : 0f;
        var curveEndTime = _nowLerpingBack ? 0f : lastFrameTime;

        while (ConditionMet(curveTime, curveEndTime))
        {
            var curveValue = Curve.Evaluate(curveTime);

            switch (LerpingType)
            {
                case LerpingObject.Transform:
                    transform.position = Vector3.Lerp(StartPosition, EndPosition, curveValue);
                    break;

                case LerpingObject.Scale:
                    transform.localScale = Vector3.Lerp(StartScale, EndScale, curveValue);
                    break;

                case LerpingObject.CameraSize:
                    Camera.main.orthographicSize = Mathf.Lerp(StartCamSize, EndCamSize, curveValue);
                    break;

                case LerpingObject.Color:

                    /// Renderer
                    if (_renderers != null && _renderers.Length > 0)
                        for (int i = 0; i < _renderers.Length; i++)
                        {
                            if (_renderers[i] != null)
                                _renderers[i].material.color = Color.Lerp(StartColor, EndColor, curveValue);
                        }

                    /// Image
                    if (_images != null && _images.Length > 0)
                        for (int i = 0; i < _images.Length; i++)
                        {
                            if (_images[i] != null)
                                _images[i].color = Color.Lerp(StartColor, EndColor, curveValue);
                        }

                    /// Text
                    if(_texts != null && _texts.Length > 0)
                        for (int i = 0; i < _texts.Length; i++)
                        {
                            if (_texts[i] != null)
                                _texts[i].color = Color.Lerp(StartColor, EndColor, curveValue);
                        }

                    break;
            }

            curveTime = _nowLerpingBack ? curveTime - Time.deltaTime * Speed : curveTime + Time.deltaTime * Speed;
            yield return null;
        }

        nowLerping = false;
        if (_nowLerpingBack || DisableGameobjectOnEnd)
        {
            gameObject.SetActive(false);
        }
        yield break;
    }

    private bool ConditionMet(float time, float endTime)
    {
        return _nowLerpingBack ? time > endTime : time < endTime;
    }
}

public enum LerpingObject
{
    Transform,
    Scale,
    Color
}