using UnityEngine;

public class BulletBehaviour : MonoBehaviour
{
    private Vector3 velocity;
    private float lifeTime;
    private float currentTime;
    
    public void Initialize(Vector3 velocity, float lifeTime)
    {
        this.velocity = velocity;
        this.lifeTime = lifeTime;
        currentTime = 0f;
    }
    
    private void Update()
    {
        // 更新位置
        transform.position += velocity * Time.deltaTime;
        
        // 更新生命时间
        currentTime += Time.deltaTime;
        if (currentTime >= lifeTime)
        {
            Destroy(gameObject);
            return;
        }
        
        // 射线检测碰撞
        if (Physics.Raycast(transform.position, velocity.normalized, out RaycastHit hit, velocity.magnitude * Time.deltaTime))
        {
            // 可以在这里添加击中特效
            Destroy(gameObject);
        }
    }
}