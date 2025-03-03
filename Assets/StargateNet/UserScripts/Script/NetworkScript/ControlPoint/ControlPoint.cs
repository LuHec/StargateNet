using System.Collections;
using System.Collections.Generic;
using StargateNet;
using UnityEngine;

public class ControlPoint : NetworkBehavior
{
    public int controlPointId;
    public int addPointPerSecond = 1;
    [Replicated]
    public float Percent { get; set; }
    HashSet<AttributeComponent> players;
    BattleManager battleManager;
    bool localPlayerIn = false;

    public override void NetworkStart(SgNetworkGalaxy galaxy)
    {
        battleManager = galaxy.FindSceneComponent<BattleManager>();
        players = new HashSet<AttributeComponent>(16);
    }

    public override void NetworkFixedUpdate(SgNetworkGalaxy galaxy)
    {
        if (IsClient) return;
        if (players.Count == 0) return;
        Percent = Mathf.Clamp(Percent + addPointPerSecond * galaxy.FixedDeltaTime, 0, 100);
    }

    [NetworkCallBack(nameof(Percent), true)]
    public void OnPercentChanged(CallbackData callbackData)
    {
        if (IsServer) return;
        if (localPlayerIn)
            UIManager.Instance.GetUIPanel<UIBattleInterface>().SetPercent(Percent);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.TryGetComponent(out FPSController fpsController))
        {
            if (IsServer)
                players.Add(fpsController.attributeComponent);
            if (IsClient && fpsController.IsLocalPlayer())
            {
                UIManager.Instance.GetUIPanel<UIBattleInterface>().ShowControlPoint(controlPointId);
                localPlayerIn = true;
            }
        }
    }

    

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.TryGetComponent(out FPSController fpsController))
        {
            if (IsServer)
                players.Remove(fpsController.attributeComponent);
            if (IsClient && fpsController.IsLocalPlayer())
            {
                UIManager.Instance.GetUIPanel<UIBattleInterface>().HideControlPoint();
                localPlayerIn = false;
            }
        }
    }
}
