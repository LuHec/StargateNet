namespace StargateNet
{
    public enum ServerToClientId : ushort
    {
        sync = 1,
        activeScene,
        playerSpawned,
        playerMovement,
        playerHealthChanged,
        playerActiveWeaponUpdated,
        playerAmmoChanged,
        playerDied,
        playerRespawned,
        projectileSpawned,
        projectileMovement,
        projectileCollided,
        projectileHitmarker,
    }

    public enum ClientToServerId : ushort
    {
        name = 1,
        input,
        switchActiveWeapon,
        primaryUse,
        reload,
    }

    public enum Protocol : ushort
    {
        ToServer = 1,
        ToClient = 2,
    }
}