using UnityEngine;

namespace StargateNet
{
    public class TransformErrorCorrectVol2
    {
        /// <summary>
        /// 当前用于渲染的Transform    
        /// </summary>
        public Vector3 CurrentPosition { private get; set; }

        public Quaternion CurrentRotation { private get; set; }
        public Vector3 PreRollbackPosition { private get; set; }
        public Quaternion PreRollbackRotation { private get; set; }
        public float Error { private get; set; }

        public void Init(Vector3 position, Quaternion rotation)
        {
            this.CurrentPosition = position;
            this.CurrentRotation = rotation;
            this.PreRollbackPosition = position;
            this.PreRollbackRotation = rotation;
            this.Error = 0f;
        }

        /// <summary>
        /// 回滚前缓存预测的位置
        /// </summary>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        public void OnPreRollback(Vector3 position, Quaternion rotation)
        {
            this.PreRollbackPosition = position;
            this.PreRollbackRotation = rotation;
        }

        /// <summary>
        /// 回滚后计算误差
        /// </summary>
        public void OnPostResimulation(Vector3 position, Quaternion rotation)
        {
            this.Error += (position - PreRollbackPosition).magnitude;
        }

        /// <summary>
        /// 用于纠正客户端的Transform错误，并将这个过程平滑
        /// </summary>
        public void Render(ref Vector3 correctRenderPosition,
            ref Quaternion correctRenderRotation,
            float maxErrorMagnitude,
            float correctionMultiplier,
            float maxBlendAlpha,
            float deltaTime)
        {
            if (this.Error >= maxErrorMagnitude)
            {
                this.Error = 0f;
                this.CurrentPosition = correctRenderPosition;
            }

            this.Error = Mathf.Max(this.Error - Mathf.Max(this.Error, 0.1f) * correctionMultiplier * deltaTime, 1E-09f);
            float alpha = 1.0f / this.Error * correctionMultiplier * deltaTime;
            if (this.Error > 0.5f)
            {
                alpha = Mathf.Min(alpha, maxBlendAlpha);
            }
            this.CurrentPosition = Vector3.Lerp(this.CurrentPosition, correctRenderPosition, alpha);
            correctRenderPosition = this.CurrentPosition;
        }
    }
}