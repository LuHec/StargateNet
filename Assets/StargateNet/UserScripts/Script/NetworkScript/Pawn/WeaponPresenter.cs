using UnityEngine;


    public class WeaponPresenter
    {
        private readonly Transform _handPoint;
        private Transform _weaponModelTransform;  // 添加武器模型的Transform引用
        private Vector3 _originalWeaponRotation;  // 保存武器原始旋转值

        [Header("Sway")]
        public bool sway = true;
        public float smooth = 10f;
        public float step = .01f;
        public float maxStepDistance = .06f;
        private Vector3 _swayPos;

        [Header("Sway Rotation")] 
        public bool swayRotation = true;
        public float smoothRot = 12f;
        public float rotationStep = 4f;
        public float maxRotationStep = 5f;
        private Vector3 _swayEulerRot;

        [Header("Bobbing")]
        public bool bobOffset = true;
        public float speedCurveMultiplier = 1f;
        private float _speedCurve;
        public Vector3 travelLimit = Vector3.one * .025f;
        public Vector3 bobLimit = Vector3.one * .1f;
        private Vector3 _bobPosition;

        [Header("Bob Rotation")]
        public bool bobSway = true;
        public Vector3 multiplier;
        private Vector3 _bobRotation;

        [Header("Visual Recoil")]
        public float visualRecoilPosStrength = 0.5f;
        public float visualRecoilRotStrength = 15f;
        public float visualRecoilPosReturnSpeed = 8f;
        public float visualRecoilRotReturnSpeed = 15f;
        public float visualRecoilMaxPos = 0.5f;
        public float visualRecoilMaxRot = 30f;
        public Vector2 visualRecoilBackwardRange = new Vector2(-0.8f, -0.5f);
        public Vector2 visualRecoilUpwardRange = new Vector2(0.2f, 0.4f);

        [Header("Visual Recoil Rotation")]
        public Vector2 recoilRotationRangeX = new Vector2(-20f, -15f);  // X轴旋转范围(上抬)
        public Vector2 recoilRotationRangeY = new Vector2(-7f, 7f);     // Y轴旋转范围(左右)
        public Vector2 recoilRotationRangeZ = new Vector2(-10f, 10f);   // Z轴旋转范围(倾斜)
    
        private Vector3 _targetRecoilPos;
        private Vector3 _currentRecoilPos;
        private Vector3 _targetRecoilRot;
        private Vector3 _currentRecoilRot;

        private float _reloadRotation;
        private Vector3 _originalRotation;

        private float CurveSin => Mathf.Sin(_speedCurve);
        private float CurveCos => Mathf.Cos(_speedCurve);

        public WeaponPresenter(Transform handPoint)
        {
            _handPoint = handPoint;
            _originalRotation = handPoint.localEulerAngles;
        }

        public void SetWeaponModel(Transform weaponModel)
        {
            _weaponModelTransform = weaponModel;
            if (_weaponModelTransform != null)
            {
                _originalWeaponRotation = _weaponModelTransform.localEulerAngles;
            }
        }

        public void SetReloadRotation(float angle)
        {
            if (_weaponModelTransform != null)
            {
                Vector3 rotation = _originalWeaponRotation;
                rotation.x = angle * 3;
                _weaponModelTransform.localEulerAngles = rotation;
            }
        }

        public void UpdatePresentation(Vector2 deltaYawPitch, Vector2 moveInput, float velocity, bool isGrounded)
        {
            Sway(deltaYawPitch);
            SwayRotation(deltaYawPitch);
            BobOffset(moveInput, velocity, isGrounded);
            BobRotation(moveInput);
            CompositePositionRotation();

            // 在最后应用换弹旋转
            Vector3 finalRotation = _handPoint.localEulerAngles;
            finalRotation.z = _reloadRotation;
            _handPoint.localEulerAngles = finalRotation;

            UpdateVisualRecoil();

            // 在CompositePositionRotation中应用视觉后坐力
            _handPoint.localPosition = Vector3.Lerp(_handPoint.localPosition, 
                _swayPos + _bobPosition + _currentRecoilPos, 
                Time.deltaTime * smooth);

            _handPoint.localRotation = Quaternion.Slerp(_handPoint.localRotation,
                Quaternion.Euler(_swayEulerRot + _currentRecoilRot) * Quaternion.Euler(_bobRotation),
                Time.deltaTime * smoothRot);
        }

        public void ApplyVisualRecoil()
        {
            // 增强的位移后坐力
            _targetRecoilPos += new Vector3(
                Random.Range(-0.1f, 0.1f),
                Random.Range(visualRecoilUpwardRange.x, visualRecoilUpwardRange.y),    // 更强的上移
                Random.Range(visualRecoilBackwardRange.x, visualRecoilBackwardRange.y)  // 更强的后移
            ) * visualRecoilPosStrength;

            // 使用配置的旋转范围
            _targetRecoilRot += new Vector3(
                Random.Range(recoilRotationRangeX.x, recoilRotationRangeX.y),   // 上抬角度
                Random.Range(recoilRotationRangeY.x, recoilRotationRangeY.y),   // 左右摇摆
                Random.Range(recoilRotationRangeZ.x, recoilRotationRangeZ.y)    // 倾斜角度
            ) * visualRecoilRotStrength;

            // 使用二阶阻尼来模拟更真实的后坐力
            float dampingFactor = 0.8f;
            _currentRecoilPos = Vector3.Lerp(_currentRecoilPos, _targetRecoilPos, 
                Time.deltaTime * visualRecoilPosReturnSpeed * dampingFactor);
            _currentRecoilRot = Vector3.Lerp(_currentRecoilRot, _targetRecoilRot, 
                Time.deltaTime * visualRecoilRotReturnSpeed * dampingFactor);

            // 限制最大位移和旋转
            _targetRecoilPos = Vector3.ClampMagnitude(_targetRecoilPos, visualRecoilMaxPos);
            _targetRecoilRot = Vector3.ClampMagnitude(_targetRecoilRot, visualRecoilMaxRot);
        }

        private void UpdateVisualRecoil()
        {
            // 位移回归
            _targetRecoilPos = Vector3.Lerp(_targetRecoilPos, Vector3.zero, Time.deltaTime * visualRecoilPosReturnSpeed);
            _currentRecoilPos = Vector3.Lerp(_currentRecoilPos, _targetRecoilPos, Time.deltaTime * visualRecoilPosReturnSpeed);

            // 旋转回归
            _targetRecoilRot = Vector3.Lerp(_targetRecoilRot, Vector3.zero, Time.deltaTime * visualRecoilRotReturnSpeed);
            _currentRecoilRot = Vector3.Lerp(_currentRecoilRot, _targetRecoilRot, Time.deltaTime * visualRecoilRotReturnSpeed);
        }

        private void Sway(Vector2 input)
        {
            if (!sway) return;
            Vector3 invertLook = new Vector3(input.x, input.y, 0) * -step;
            invertLook.x = Mathf.Clamp(invertLook.x, -maxStepDistance, maxStepDistance);
            invertLook.y = Mathf.Clamp(invertLook.y, -maxStepDistance, maxStepDistance);

            _swayPos = invertLook;
        }

        private void SwayRotation(Vector2 input)
        {
            if (!swayRotation) return;
            Vector2 invertLook = input * -rotationStep;
            invertLook.x = Mathf.Clamp(invertLook.x, -maxRotationStep, maxRotationStep);
            invertLook.y = Mathf.Clamp(invertLook.y, -maxRotationStep, maxRotationStep);

            _swayEulerRot = new Vector3(invertLook.y, invertLook.x, invertLook.x);
        }

        private void BobOffset(Vector2 input, float velocity, bool isGrounded)
        {
            _speedCurve += Time.deltaTime * (isGrounded ? velocity * speedCurveMultiplier : 1f) + .01f;
            if (!bobOffset)
            {
                _bobPosition = Vector3.zero;
                return;
            }

            _bobPosition.x = CurveCos * bobLimit.x * (isGrounded ? 1 : 0) - input.x * travelLimit.x;
            _bobPosition.y = CurveSin * bobLimit.y * (isGrounded ? 1 : 0) - input.y * travelLimit.y;
            _bobPosition.z = -(input.y * travelLimit.z);
        }

        private void BobRotation(Vector2 input)
        {
            if (!bobSway)
            {
                _bobRotation = Vector3.zero;
                return;
            }

            _bobRotation.x = input != Vector2.zero
                ? multiplier.x * (Mathf.Sin(2 * _speedCurve))
                : multiplier.x * (Mathf.Sin(2 * _speedCurve) / 2);
            _bobRotation.y = input != Vector2.zero ? multiplier.y * CurveCos : 0;
            _bobRotation.z = input != Vector2.zero ? multiplier.z * CurveCos * input.x : 0;
        }

        private void CompositePositionRotation()
        {
            _handPoint.localPosition = 
                Vector3.Lerp(_handPoint.localPosition, _swayPos + _bobPosition, Time.deltaTime * smooth);
            _handPoint.localRotation = Quaternion.Slerp(_handPoint.localRotation,
                Quaternion.Euler(_swayEulerRot) * Quaternion.Euler(_bobRotation),
                Time.deltaTime * smoothRot);
        }
    }
