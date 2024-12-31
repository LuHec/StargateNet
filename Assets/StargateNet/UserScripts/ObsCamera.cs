using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObsCamera : MonoBehaviour
{
    public float moveSpeed = 10f; // 摄像机的移动速度
    public float verticalSpeed = 5f; // 上下移动的速度
    public float sensitivity = 2f; // 鼠标灵敏度

    private float horizontalInput, verticalInput, upDownInput;
    private Camera _camera;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        _camera = Camera.main;
    }

    void Update()
    {
        if (Input.GetKey(KeyCode.LeftAlt))
        {
            Cursor.lockState = CursorLockMode.None;
            return;
        }
        else 
            Cursor.lockState = CursorLockMode.Locked;
        // 获取水平和垂直方向的输入
        horizontalInput = Input.GetAxis("Horizontal");  // A/D 或箭头左右
        verticalInput = Input.GetAxis("Vertical");  // W/S 或箭头上下
        upDownInput = 0;

        // 使用空格和Ctrl控制上下移动
        if (Input.GetKey(KeyCode.Space))  // 空格键向上
        {
            upDownInput = 1;
        }
        else if (Input.GetKey(KeyCode.LeftControl))  // Ctrl键向下
        {
            upDownInput = -1;
        }

        // 获取摄像机当前的前、右、上方向
        Vector3 forward = _camera.transform.forward;
        Vector3 right = _camera.transform.right;
        Vector3 up = _camera.transform.up;
        
        forward.Normalize();
        right.Normalize();

        // 根据摄像机方向进行移动
        Vector3 moveDirection = forward * verticalInput + right * horizontalInput + up * upDownInput;
        transform.Translate(moveDirection * moveSpeed * Time.deltaTime, Space.World);

        // 旋转摄像机，根据鼠标移动
        float mouseX = Input.GetAxis("Mouse X") * sensitivity;
        float mouseY = -Input.GetAxis("Mouse Y") * sensitivity;

        transform.Rotate(Vector3.up, mouseX, Space.World);  // 旋转摄像机水平方向
        _camera.transform.Rotate(Vector3.right, mouseY, Space.Self);  // 旋转摄像机垂直方向
    }
}