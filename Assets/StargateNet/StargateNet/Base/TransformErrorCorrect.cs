using UnityEngine;

namespace StargateNet
{
    public class TransformErrorCorrect
    {
        /// <summary>
        /// 当前用于渲染的Transform    
        /// </summary>
        public Vector3 CurrentPosition { get; set; }

        public Quaternion CurrentRotation { get; set; }
        public Vector3 PreRollbackPosition { get; set; }
        public Quaternion PreRollbackRotation { get; set; }


        public void OnPreRollback(Vector3 position, Quaternion rotation)
        {
            this.PreRollbackPosition = position;
            this.PreRollbackRotation = rotation;
        }

        public void OnPostResimulation()
        {
        }

        /// <summary>
        /// 用于纠正客户端的Transform错误，并将这个过程平滑
        /// </summary>
        /// <param name="correctRenderPosition"></param>
        /// <param name="correctRenderRotation"></param>
        /// <param name="maxErrorMagnitude"></param>
        /// <param name="correctionMultiplier"></param>
        public void Render(ref Vector3 correctRenderPosition, ref Quaternion correctRenderRotation, float maxErrorMagnitude, float correctionMultiplier)
        {
            float value = (CurrentPosition - correctRenderPosition).magnitude;
            if (value >= maxErrorMagnitude)
            {
                this.CurrentPosition = correctRenderPosition;
            }

            float alpha = Mathf.Max(Mathf.Min(1f, value / maxErrorMagnitude * correctionMultiplier), 0);
            this.CurrentPosition = Vector3.Lerp(this.CurrentPosition, correctRenderPosition, alpha);

            correctRenderPosition = this.CurrentPosition;
        }
    }
}