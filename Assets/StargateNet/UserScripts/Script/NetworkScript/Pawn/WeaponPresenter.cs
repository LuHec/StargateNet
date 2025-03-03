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
