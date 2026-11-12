using UnityEngine;

/// <summary>Player health and a minimal HUD; authored combat animations remain optional.</summary>
[RequireComponent(typeof(Player))]
public sealed class PlayerHealth : CharacterHealthBase
{
    [SerializeField] private bool showHealthHud = true;
    private Player player;
    protected override bool ShowWorldHealthBar => false;

    protected override void Awake()
    {
        base.Awake();
        player = GetComponent<Player>();
    }

    protected override void OnDied()
    {
        if (player == null)
            player = GetComponent<Player>();
        player?.OnDeath();
    }

    protected override void OnRevived()
    {
        if (player == null)
            player = GetComponent<Player>();
        player?.OnRevived();
    }

    private void OnGUI()
    {
        if (!showHealthHud)
            return;
        GUI.Box(new Rect(20f, 20f, 220f, 64f), "");
        var status = IsDead ? "  已死亡" : "";
        GUI.Label(new Rect(32f, 28f, 200f, 24f), $"HP  {Mathf.CeilToInt(CurrentHP)} / {Mathf.CeilToInt(MaxHP)}{status}");
        if (IsDead)
        {
            if (GUI.Button(new Rect(32f, 53f, 196f, 24f), "复活 / Revive"))
                Revive();
        }
        else
        {
            var width = 196f * Mathf.Clamp01(CurrentHP / MaxHP);
            var previousColor = GUI.color;
            GUI.color = new Color(0.3f, 0.85f, 0.4f);
            GUI.DrawTexture(new Rect(32f, 58f, width, 10f), Texture2D.whiteTexture);
            GUI.color = previousColor;
        }
    }
}
