using UnityEngine;

namespace YourCat.DesktopPet
{
    public sealed class DesktopPetBehaviour : MonoBehaviour
    {
        private static readonly int Speed = Animator.StringToHash("Speed");
        private static readonly int Sit = Animator.StringToHash("Sit");
        private static readonly int LieDown = Animator.StringToHash("LieDown");
        private static readonly int Sleep = Animator.StringToHash("Sleep");
        private static readonly int Petted = Animator.StringToHash("Petted");
        private static readonly int Eat = Animator.StringToHash("Eat");

        [SerializeField] private Animator animator;
        [SerializeField] private float walkSpeed = 0.65f;
        [SerializeField] private float directionChangeSeconds = 5f;
        [SerializeField] private float idleDecisionSeconds = 8f;
        [SerializeField] private float viewportPadding = 0.08f;

        private Camera mainCamera;
        private float direction = 1f;
        private float directionTimer;
        private float idleTimer;
        private float actionTimer;
        private bool dragging;
        private Vector3 dragOffset;

        private void Awake()
        {
            mainCamera = Camera.main;
            directionTimer = directionChangeSeconds;
            idleTimer = idleDecisionSeconds;
        }

        private void Update()
        {
            if (dragging)
            {
                animator.SetFloat(Speed, 0f);
                transform.position = MouseWorldPosition() + dragOffset;
                return;
            }

            if (actionTimer > 0f)
            {
                actionTimer -= Time.deltaTime;
                animator.SetFloat(Speed, 0f);
                return;
            }

            idleTimer -= Time.deltaTime;
            if (idleTimer <= 0f)
            {
                ChooseIdleAction();
                idleTimer = idleDecisionSeconds + Random.Range(-2f, 3f);
            }

            directionTimer -= Time.deltaTime;
            if (directionTimer <= 0f)
            {
                direction *= -1f;
                directionTimer = directionChangeSeconds;
                transform.Rotate(0f, 180f, 0f);
            }

            transform.position += Vector3.right * (direction * walkSpeed * Time.deltaTime);
            KeepInsideDesktop();
            animator.SetFloat(Speed, walkSpeed);
        }

        private void OnMouseDown()
        {
            dragging = true;
            actionTimer = 0f;
            dragOffset = transform.position - MouseWorldPosition();
        }

        private void OnMouseUp()
        {
            dragging = false;
        }

        private void OnMouseOver()
        {
            if (Input.GetMouseButtonDown(1))
            {
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                    Feed();
                else
                    Pet();
            }
        }

        public void SitDown() => TriggerAction(Sit, 2f);
        public void LieDownNow() => TriggerAction(LieDown, 3f);
        public void GoToSleep() => TriggerAction(Sleep, 5f);
        public void Feed() => TriggerAction(Eat, 1f);
        public void Pet() => TriggerAction(Petted, 1f);
        public void SetWalkSpeed(float value) => walkSpeed = Mathf.Clamp(value, 0.2f, 1.2f);

        private void TriggerAction(int trigger, float seconds)
        {
            animator.SetFloat(Speed, 0f);
            animator.SetTrigger(trigger);
            actionTimer = seconds;
        }

        private void ChooseIdleAction()
        {
            switch (Random.Range(0, 4))
            {
                case 0:
                    SitDown();
                    break;
                case 1:
                    LieDownNow();
                    break;
                case 2:
                    GoToSleep();
                    break;
                default:
                    direction *= -1f;
                    transform.Rotate(0f, 180f, 0f);
                    break;
            }
        }

        private void KeepInsideDesktop()
        {
            var viewport = mainCamera.WorldToViewportPoint(transform.position);
            if (viewport.x >= viewportPadding && viewport.x <= 1f - viewportPadding)
                return;

            direction = viewport.x < viewportPadding ? 1f : -1f;
            viewport.x = Mathf.Clamp(viewport.x, viewportPadding, 1f - viewportPadding);
            transform.position = mainCamera.ViewportToWorldPoint(viewport);

            var facingRight = transform.forward.z >= 0f;
            if ((direction > 0f) != facingRight)
                transform.Rotate(0f, 180f, 0f);
        }

        private Vector3 MouseWorldPosition()
        {
            var mouse = Input.mousePosition;
            mouse.z = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);
            return mainCamera.ScreenToWorldPoint(mouse);
        }
    }
}
