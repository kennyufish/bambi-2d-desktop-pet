using UnityEngine;

namespace YourCat.DesktopPet
{
    public sealed class DesktopPetBehaviour : MonoBehaviour
    {
        private static readonly int Speed = Animator.StringToHash("Speed");
        private static readonly int Sit = Animator.StringToHash("Sit");
        private static readonly int Sleep = Animator.StringToHash("Sleep");
        private static readonly int Petted = Animator.StringToHash("Petted");
        private static readonly int Eat = Animator.StringToHash("Eat");

        [SerializeField] private Animator animator;
        [SerializeField] private float walkSpeed = 0.65f;
        [SerializeField] private float directionChangeSeconds = 5f;

        private Camera mainCamera;
        private float direction = 1f;
        private float directionTimer;
        private bool dragging;
        private Vector3 dragOffset;

        private void Awake()
        {
            mainCamera = Camera.main;
            directionTimer = directionChangeSeconds;
        }

        private void Update()
        {
            if (dragging)
            {
                animator.SetFloat(Speed, 0f);
                transform.position = MouseWorldPosition() + dragOffset;
                return;
            }

            directionTimer -= Time.deltaTime;
            if (directionTimer <= 0f)
            {
                direction *= -1f;
                directionTimer = directionChangeSeconds;
                transform.Rotate(0f, 180f, 0f);
            }

            transform.position += Vector3.right * (direction * walkSpeed * Time.deltaTime);
            animator.SetFloat(Speed, walkSpeed);
        }

        private void OnMouseDown()
        {
            dragging = true;
            dragOffset = transform.position - MouseWorldPosition();
        }

        private void OnMouseUp()
        {
            dragging = false;
        }

        private void OnMouseOver()
        {
            if (Input.GetMouseButtonDown(1))
                animator.SetTrigger(Petted);
        }

        public void SitDown() => animator.SetTrigger(Sit);
        public void GoToSleep() => animator.SetTrigger(Sleep);
        public void Feed() => animator.SetTrigger(Eat);

        private Vector3 MouseWorldPosition()
        {
            var mouse = Input.mousePosition;
            mouse.z = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);
            return mainCamera.ScreenToWorldPoint(mouse);
        }
    }
}
