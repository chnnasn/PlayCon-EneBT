using UnityEngine;

[CreateAssetMenu(fileName = "TileSprites", menuName = "Puzzle/TileSprites")]
public class TileSO : ScriptableObject
{
    [Header("Sprites")]
    public Sprite normal;
    public Sprite start;
    public Sprite end;
    public Sprite middleType2;  // 竖进横出
    public Sprite middleType3;  // 横进竖出

    [Header("Layout")]
    [Range(20, 30)] public float cellSize = 20f;
    [Range(2, 4)] public float cellSpacing = 8f;

    [Header("Colors")]
    public Color containerColor = new(0f, 0f, 0f, 0f);
    public Color tileColor = new(0.15f, 0.15f, 0.15f, 0.9f);
    public Color activeColor = new(1f, 0.78f, 0.2f, 1f);
    public Color inactiveIconColor = Color.white;
    public Color activeIconColor = Color.white;
}