using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class ImageOutline : Image
{
    [Header("描边")]
    public Color outlineColor = Color.black;
    [Range(1, 50)] public float outlineWidth = 10f;

    [Header("Sprite 内缩")]
    [Min(0f)] public float spriteInset = 40f;

    [Header("运行时 Sprite PPU 兼容")]
    [Tooltip("当运行时 Sprite 的 pixelsPerUnit 过小（常见于 Sprite.Create 使用了 1）时，用该值计算 SetNativeSize，避免 UI 尺寸被撑得过大。")]
    [Min(1f)] public float fallbackPixelsPerUnit = 100f;
    [Tooltip("当 sprite.pixelsPerUnit 小于该阈值时，启用 fallbackPixelsPerUnit。")]
    [Min(0f)] public float minAcceptableSpritePixelsPerUnit = 10f;

    protected override void Start()
    {
        base.Start();
        raycastTarget = false;
        type = Image.Type.Simple;
    }

    public override void SetNativeSize()
    {
        if (sprite == null)
        {
            base.SetNativeSize();
            return;
        }

        var ppu = sprite.pixelsPerUnit;
        if (ppu <= 0f || ppu < minAcceptableSpritePixelsPerUnit)
            ppu = fallbackPixelsPerUnit;

        var size = sprite.rect.size / ppu;
        rectTransform.sizeDelta = size;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect rect = rectTransform.rect;
        float w = rect.width;
        float h = rect.height;

        Vector2 pivotOffset = new Vector2(
            -rectTransform.pivot.x * w,
            -rectTransform.pivot.y * h
        );

        // --- 1. 描边（四边 + 四角，逆时针 CCW） ---
        float lw = Mathf.Max(outlineWidth, 1f);
        float left   = pivotOffset.x;
        float right  = pivotOffset.x + w;
        float bottom = pivotOffset.y;
        float top    = pivotOffset.y + h;

        // 顶边
        AddQuad(vh,
            new Vector3(left + lw, top - lw, 0),
            new Vector3(left + lw, top, 0),
            new Vector3(right - lw, top, 0),
            new Vector3(right - lw, top - lw, 0), outlineColor);

        // 底边
        AddQuad(vh,
            new Vector3(left + lw, bottom, 0),
            new Vector3(left + lw, bottom + lw, 0),
            new Vector3(right - lw, bottom + lw, 0),
            new Vector3(right - lw, bottom, 0), outlineColor);

        // 左边
        AddQuad(vh,
            new Vector3(left, bottom + lw, 0),
            new Vector3(left, top - lw, 0),
            new Vector3(left + lw, top - lw, 0),
            new Vector3(left + lw, bottom + lw, 0), outlineColor);

        // 右边
        AddQuad(vh,
            new Vector3(right - lw, bottom + lw, 0),
            new Vector3(right - lw, top - lw, 0),
            new Vector3(right, top - lw, 0),
            new Vector3(right, bottom + lw, 0), outlineColor);

        // 左上角
        AddQuad(vh,
            new Vector3(left, top - lw, 0),
            new Vector3(left, top, 0),
            new Vector3(left + lw, top, 0),
            new Vector3(left + lw, top - lw, 0), outlineColor);

        // 右上角
        AddQuad(vh,
            new Vector3(right - lw, top - lw, 0),
            new Vector3(right - lw, top, 0),
            new Vector3(right, top, 0),
            new Vector3(right, top - lw, 0), outlineColor);

        // 左下角
        AddQuad(vh,
            new Vector3(left, bottom, 0),
            new Vector3(left, bottom + lw, 0),
            new Vector3(left + lw, bottom + lw, 0),
            new Vector3(left + lw, bottom, 0), outlineColor);

        // 右下角
        AddQuad(vh,
            new Vector3(right - lw, bottom, 0),
            new Vector3(right - lw, bottom + lw, 0),
            new Vector3(right, bottom + lw, 0),
            new Vector3(right, bottom, 0), outlineColor);

        // --- 2. Sprite（覆盖在描边内部） ---
        if (sprite != null)
        {
            Texture tex = mainTexture;
            if (tex != null)
            {
                float inset = Mathf.Max(spriteInset, 0f);

                float sLeft   = left + inset;
                float sRight  = right - inset;
                float sBottom = bottom + inset;
                float sTop    = top - inset;

                if (sRight > sLeft && sTop > sBottom)
                {
                    // Keep sprite aspect ratio instead of stretching/cropping it to the whole tile.
                    float targetW = sRight - sLeft;
                    float targetH = sTop - sBottom;
                    float spriteW = sprite.rect.width;
                    float spriteH = sprite.rect.height;

                    if (spriteW > 0f && spriteH > 0f)
                    {
                        float spriteAspect = spriteW / spriteH;
                        float targetAspect = targetW / targetH;

                        if (spriteAspect > targetAspect)
                        {
                            float drawH = targetW / spriteAspect;
                            float pad = (targetH - drawH) * 0.5f;
                            sBottom += pad;
                            sTop -= pad;
                        }
                        else
                        {
                            float drawW = targetH * spriteAspect;
                            float pad = (targetW - drawW) * 0.5f;
                            sLeft += pad;
                            sRight -= pad;
                        }
                    }

                    Rect spriteRect = sprite.textureRect;
                    float texW = sprite.texture.width;
                    float texH = sprite.texture.height;

                    Vector2 uvMin = new Vector2(spriteRect.x / texW, spriteRect.y / texH);
                    Vector2 uvMax = new Vector2(
                        (spriteRect.x + spriteRect.width) / texW,
                        (spriteRect.y + spriteRect.height) / texH);

                    Color spriteCol = base.color;
                    AddQuadWithUV(vh,
                        new Vector3(sLeft, sBottom, 0),
                        new Vector3(sLeft, sTop, 0),
                        new Vector3(sRight, sTop, 0),
                        new Vector3(sRight, sBottom, 0),
                        uvMin, uvMax, spriteCol);
                }
            }
        }
    }

    private void AddQuad(VertexHelper vh, Vector3 bl, Vector3 tl, Vector3 tr, Vector3 br, Color col)
    {
        // 边框纯色，UV 用固定值 (0.5, 0.5)，不影响颜色
        UIVertex v0 = NewVertex(bl, new Vector2(0.5f, 0.5f), col);
        UIVertex v1 = NewVertex(tl, new Vector2(0.5f, 0.5f), col);
        UIVertex v2 = NewVertex(tr, new Vector2(0.5f, 0.5f), col);
        UIVertex v3 = NewVertex(br, new Vector2(0.5f, 0.5f), col);
        vh.AddUIVertexQuad(new[] { v0, v1, v2, v3 });
    }

    private void AddQuadWithUV(VertexHelper vh, Vector3 bl, Vector3 tl, Vector3 tr, Vector3 br,
        Vector2 uvMin, Vector2 uvMax, Color col)
    {
        UIVertex v0 = NewVertex(bl, new Vector2(uvMin.x, uvMin.y), col);
        UIVertex v1 = NewVertex(tl, new Vector2(uvMin.x, uvMax.y), col);
        UIVertex v2 = NewVertex(tr, new Vector2(uvMax.x, uvMax.y), col);
        UIVertex v3 = NewVertex(br, new Vector2(uvMax.x, uvMin.y), col);
        vh.AddUIVertexQuad(new[] { v0, v1, v2, v3 });
    }

    private UIVertex NewVertex(Vector3 pos, Vector2 uv, Color col)
    {
        UIVertex v = new UIVertex();
        v.position = pos;
        v.uv0 = uv;
        v.color = col;
        v.uv1 = Vector2.zero;
        v.uv2 = Vector2.zero;
        v.uv3 = Vector2.zero;
        v.normal = Vector3.back;
        v.tangent = new Vector4(1f, 0f, 0f, 1f);
        return v;
    }
}
