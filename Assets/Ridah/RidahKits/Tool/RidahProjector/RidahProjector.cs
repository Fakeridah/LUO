using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Renderer)), RequireComponent(typeof(MeshFilter))]
public class RidahProjector : MonoBehaviour
{
    new Renderer renderer;
    Matrix4x4 projectionMatrix;
    Matrix4x4 inverseProjection;
    MaterialPropertyBlock mpb;

    int PropTexture = Shader.PropertyToID("_Texture");
    int PropProjection = Shader.PropertyToID("_Projection");
    int PropInverse = Shader.PropertyToID("_Inverse");
    int PropNear = Shader.PropertyToID("_Near");
    int PropFar = Shader.PropertyToID("_Far");
    int PropFov = Shader.PropertyToID("_Fov");
    int PropAspect = Shader.PropertyToID("_Aspect");
    int PropColor = Shader.PropertyToID("_Color");

    public Texture SourceTexture => SourceTexture;
    [SerializeField] Texture sourceTexture;
    [SerializeField] float fieldOfView = 65f;
    [SerializeField] float aspectRatio = 1.6f / 0.9f;
    [SerializeField] float nearClip = 0.1f;
    [SerializeField] float farClip = 100f;
    [SerializeField, ColorUsage(false, true)] Color projectionColor = Color.white;
    //[SerializeField] Vector2 bound = new Vector2(0,0);

    private void OnValidate()
    {
        Setup();
    }
    private void Start()
    {
        Setup();
        var size =  farClip - nearClip;
        GetComponent<MeshFilter>().mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000);
    }

    [ContextMenu("setup")]
    void Setup()
    {
        renderer = GetComponent<Renderer>();
        if (mpb == null)
            mpb = new MaterialPropertyBlock();

        projectionMatrix = Matrix4x4.Perspective(fieldOfView, aspectRatio, nearClip, farClip);
        inverseProjection = Matrix4x4.Inverse(projectionMatrix);

        renderer.GetPropertyBlock(mpb);
        mpb.SetTexture(PropTexture, sourceTexture);
        mpb.SetMatrix(PropProjection, projectionMatrix);
        mpb.SetMatrix(PropInverse, inverseProjection);
        mpb.SetFloat(PropNear, nearClip);
        mpb.SetFloat(PropFar, farClip);
        mpb.SetColor(PropColor, projectionColor);
        renderer.SetPropertyBlock(mpb);

    }
}
