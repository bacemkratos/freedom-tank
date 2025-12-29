using UnityEngine;

public class PlayerManager : MonoBehaviour
{
    public int maxHp = 10;
    public int hp;

    private void Awake()
    {
        hp = maxHp;
    }

    private void OnEnable()
    {
        EventBus.Subscribe<PlayerHitEvent>(OnPlayerHit);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<PlayerHitEvent>(OnPlayerHit);
    }

    private void OnPlayerHit(PlayerHitEvent e)
    {
        hp -= e.damage;
        Debug.Log($"[PlayerManager] Hit! -{e.damage} HP. Now HP={hp}");

        // later: UI update, invincibility frames, screen shake, etc.
        if (hp <= 0)
        {
            Debug.Log("[PlayerManager] Player dead");
            // raise PlayerDiedEvent, trigger game over, etc.
        }
    }
}
