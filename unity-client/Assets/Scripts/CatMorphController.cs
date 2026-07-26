using UnityEngine;

namespace YourCat.DesktopPet
{
    public sealed class CatMorphController : MonoBehaviour
    {
        [SerializeField] private Transform torso;
        [SerializeField] private Transform head;
        [SerializeField] private Transform leftEar;
        [SerializeField] private Transform rightEar;
        [SerializeField] private Transform[] legs;

        private Vector3 torsoBaseScale;
        private Vector3 headBaseScale;
        private Vector3 leftEarBaseScale;
        private Vector3 rightEarBaseScale;
        private Vector3[] legBaseScales;

        private void Awake()
        {
            torsoBaseScale = torso.localScale;
            headBaseScale = head.localScale;
            leftEarBaseScale = leftEar.localScale;
            rightEarBaseScale = rightEar.localScale;
            legBaseScales = new Vector3[legs.Length];

            for (var index = 0; index < legs.Length; index++)
                legBaseScales[index] = legs[index].localScale;
        }

        public void Apply(CatShape shape)
        {
            torso.localScale = Vector3.Scale(
                torsoBaseScale,
                new Vector3(
                    Mathf.Lerp(0.85f, 1.2f, shape.weight),
                    Mathf.Lerp(0.9f, 1.12f, shape.weight),
                    Mathf.Lerp(0.85f, 1.2f, shape.weight)
                )
            );

            var faceScale = Mathf.Lerp(0.85f, 1.15f, shape.faceWidth);
            head.localScale = Vector3.Scale(headBaseScale, new Vector3(faceScale, 1f, faceScale));

            var earScale = Mathf.Lerp(0.8f, 1.2f, shape.earSize);
            leftEar.localScale = leftEarBaseScale * earScale;
            rightEar.localScale = rightEarBaseScale * earScale;

            var legScale = Mathf.Lerp(0.78f, 1.2f, shape.legLength);
            for (var index = 0; index < legs.Length; index++)
            {
                var scale = legBaseScales[index];
                scale.y *= legScale;
                legs[index].localScale = scale;
            }
        }
    }
}
