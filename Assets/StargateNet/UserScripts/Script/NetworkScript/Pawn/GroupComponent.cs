using StargateNet;
using UnityEngine;

public class GroupComponent : NetworkBehavior 
{
    [Replicated] public int GroupId { get; set; }

    [NetworkCallBack(nameof(GroupId), true)]
    public void OnGroupIdChanged(CallbackData callbackData)
    {
        Debug.LogWarning($"Change GroupId To {GroupId}");
    }
}
