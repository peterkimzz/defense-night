using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayController : MonoBehaviour {

    /* 
        보호 수준은 유지되면서 인스펙터 창에선 수정이 가능하다.
        데이터 직렬화. 세이브할 때 유용함.
    */

    // 스피드 조정 변수
    [SerializeField]
    private float walkSpeed;
    [SerializeField]
    private float runSpeed;
    private float applySpeed;
    [SerializeField]
    private float crouchSpeed;

    [SerializeField]
    private float jumpForce;

    // 상태 변수
    private bool isRun = false;
    private bool isCrouch = false;
    private bool isGround = true;

    // 앉았을 때 얼마나 앉을지 결정하는 변수.
    [SerializeField]
    private float crouchPosY;
    private float originPosY;
    private float applyCrouchPosY;

    // 땅 착지 여부를 알기 위해
    private CapsuleCollider capusleCollider;

    // 민감도
    [SerializeField]
    private float lookSensitivity;

    // 카메라 한계
    [SerializeField]
    private float cameraRotationLimit;
    private float currentCameraRotationX = 0;

    // 필요한 컴포넌트
    [SerializeField]
    private Camera theCamera;
    private Rigidbody myRigid;

    // Start is called before the first frame update
    void Start() {
        // 이 방법은 모든 하이어라키에서 카메라 타입을 가져온다는 뜻인데 이건 카메라가 2개 이상이면 안되는 방법이니 유일하지 않으면 비추
        // theCamera = FindObjectOfType<Camera>();

        /*
            초기화 부분은 1. 인스펙터에서 끌어 당겨서 하는 방법이랑
            2. 이렇게 Start() 안에서 하는 방법 2가지가 있는데
            유니티는 2번을 권장하고 있음.
        */
        capusleCollider = GetComponent<CapsuleCollider>();
        myRigid = GetComponent<Rigidbody>();
        applySpeed = walkSpeed;
        originPosY = theCamera.transform.localPosition.y;
        applyCrouchPosY = originPosY;
    }

    // Update is called once per frame
    void Update() {

        IsGround();
        TryJump();
        TryRun();
        TryCrouch();
        Move();
        CameraRotation();
        CharacterRotation();
    }

    // 앉기 시도
    private void TryCrouch() {
        if (Input.GetKeyDown(KeyCode.LeftControl)) {
            Crouch();
        }
    }

    // 앉기
    private void Crouch() {
        isCrouch = !isCrouch;

        if (isCrouch) {
            applySpeed = crouchSpeed;
            applyCrouchPosY = crouchPosY;
        } else {
            applySpeed = walkSpeed;
            applyCrouchPosY = originPosY;
        }

        StartCoroutine(CrouchCoroutine());
    }

    // 코루틴의 역할 -> 비동기 코드
    // 부드러운 앉기/서기 동작
    IEnumerator CrouchCoroutine() {
        float _posY = theCamera.transform.localPosition.y;
        int count = 0;

        while (_posY != applyCrouchPosY) {
            // 보간. easing로 늘어나게끔 하는거임.
            _posY = Mathf.Lerp(_posY, applyCrouchPosY, 0.5f);
            theCamera.transform.localPosition = new Vector3(0, _posY, 0);

            if (count > 15) break;

            // 매 프레임 마다 실행한다는 뜻
            yield return null;
        }

        theCamera.transform.localPosition = new Vector3(0, applyCrouchPosY, 0);
    }

    // 지면 체크
    private void IsGround() {
        /*
            첫 번째 인자는 어디에서 쏠 건지
            두 번째 인자에서 -transform.up을 써도 되는데 이 transform은 gameObject가 어떤 이유에서든지 뒤집어지거나 했을 때는 방향이 바뀔 수 있다.
            그러니까 어떤 상황에서도 아래로 광선을 쏘도록 Vector.down (0, -1, 0)을 사용한다.
            세 번째 인자는 콜라이더의 y값 바운더리의 extents (절반) 만큼 길이로 쏜다는 뜻이 된다.

            그래서 결국에 Raycast 함수는 true or false를 반환하는 거임.

            세 번째에서 + 0.1f같은 경우는 계단이나 그렇 곳에 닿아있을 때는 약간의 오차로 플레이어가 떠있다는 판정을 받을 수도 있어서 약간만 더 광선의 길이를 늘려줌.
        */
        isGround = Physics.Raycast(transform.position, Vector3.down, capusleCollider.bounds.extents.y + 0.1f);
    }

    // 점프 시도
    private void TryJump() {

        // 땅에 있는 상태에서 스페이스바를 눌렀을 때
        if (Input.GetKeyDown(KeyCode.Space) && isGround) {
            Jump();
        }
    }

    // 점프
    private void Jump() {

        // 앉은 상태에서 점프를 하면 점프 후에 앉은 상태 해제
        if (isCrouch) Crouch();

        myRigid.velocity = transform.up * jumpForce;
    }

    // 달리기 시도
    private void TryRun() {

        // 왼쪽 시프트키를 누르고 있는 상태면 계속 호출 됨
        if (Input.GetKey(KeyCode.LeftShift)) {
            Running();
        }

        // 왼쪽 시프트키를 뗐을 때
        if (Input.GetKeyUp(KeyCode.LeftShift)) {
            RunningCancel();
        }
    }

    // 달리기
    private void Running() {
        // 앉은 상태에서 달리기를 하면 앉음을 해제하고 달림.
        if (isCrouch) Crouch();

        isRun = true;
        applySpeed = runSpeed;
    }

    // 달리기 취소
    private void RunningCancel() {
        isRun = false;
        applySpeed = walkSpeed;
    }

    // 이동
    private void Move() {
        /*  
            GetAxisRaw("Horizontal")은 a, d 혹은 마우스 왼쪽 오른쪽 4개를 받아서 -1 ~ 1 까지 반환하는 역할을 함
        */
        // 간단히 말하면 X는 좌우 Y는 앞뒤
        float _moveDirX = Input.GetAxisRaw("Horizontal");
        float _moveDirZ = Input.GetAxisRaw("Vertical");

        // transform.right는 (1, 0, 0)을 뜻함.
        Vector3 _moveHorizontal = transform.right * _moveDirX;

        // transform.forward는 (0, 0, 1)을 뜻함.
        Vector3 _moveVertical = transform.forward * _moveDirZ;

        // 두 벡터를 합치기
        /*
            (1, 0, 0) + (0, 0, 1) = (1, 0, 1)이 되어서 총 합이 2가 되는데
            normalized는 안의 값을 벡터의 총 합을 1로 만들어 준다. 삼각함수를 생각하면 됨.
            아래의 케이스에선 (0.5, 0, 0.5) = 1이 되는 것.
            속도를 정규화해서 계산하기 편하게 해주고
            유니티에서도 이게 최적화 측면에서 좋다고 함.
        */
        Vector3 _velocity = (_moveHorizontal + _moveVertical).normalized * applySpeed;

        // Time.deltaTime이 없으면 캐릭터가 순간이동하는 것 처럼 보이니까 매 프레임에 나눠서 이동하게끔 하는 역할임.
        myRigid.MovePosition(transform.position + _velocity * Time.deltaTime);
    }

    // 위 아래 카메라 회전
    private void CameraRotation() {
        /*  
            마우스는 2차원이기 때문에 위 아래로 마우스를 움직이는 행위가 Y값임.
            이 값은 -1 ~ 1을 넘을 수 있다.
        */
        float _xRotation = Input.GetAxisRaw("Mouse Y");
        float _cameraRotationX = _xRotation * lookSensitivity;
        currentCameraRotationX -= _cameraRotationX;
        currentCameraRotationX = Mathf.Clamp(currentCameraRotationX, -cameraRotationLimit, cameraRotationLimit);

        theCamera.transform.localEulerAngles = new Vector3(currentCameraRotationX, 0, 0);
    }

    // 좌우 캐릭터 회전
    private void CharacterRotation() {
        float _zRotation = Input.GetAxisRaw("Mouse X");
        Vector3 _characterRotationY = new Vector3(0f, _zRotation, 0f) * lookSensitivity;
        myRigid.MoveRotation(myRigid.rotation * Quaternion.Euler(_characterRotationY));
    }
}