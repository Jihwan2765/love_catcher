using UnityEngine;
using System;
using System.Collections.Generic;

namespace ClawMachine.Mechanics
{
    public class GoalBoxTrigger : MonoBehaviour
    {
        public static event Action<GameObject> OnDollScored;

        [Header("Tag Settings")]
        [Tooltip("인형 콜라이더에 달릴 태그")]
        public string dollTag = "Doll";

        /// <summary>
        /// Goal 영역 안에 남아 있는 하트를 중복 없이 조회합니다.
        /// </summary>
        public List<GameObject> GetDollsInsideGoal()
        {
            var dolls = new HashSet<GameObject>();
            BoxCollider goalArea = GetComponent<BoxCollider>();
            if (goalArea == null)
            {
                Debug.LogWarning("[Goal 검사] BoxCollider를 찾을 수 없어 잔존 하트 검사를 건너뜁니다.");
                return new List<GameObject>();
            }

            Physics.SyncTransforms();

            Vector3 scale = goalArea.transform.lossyScale;
            Vector3 halfExtents = Vector3.Scale(
                goalArea.size * 0.5f,
                new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            Vector3 center = goalArea.transform.TransformPoint(goalArea.center);

            Collider[] overlaps = Physics.OverlapBox(
                center,
                halfExtents,
                goalArea.transform.rotation,
                Physics.AllLayers,
                QueryTriggerInteraction.Collide);

            foreach (var overlap in overlaps)
            {
                GameObject candidate = overlap.attachedRigidbody != null
                    ? overlap.attachedRigidbody.gameObject
                    : overlap.gameObject;

                if (candidate.CompareTag(dollTag))
                {
                    dolls.Add(candidate);
                }
                else if (overlap.CompareTag(dollTag))
                {
                    dolls.Add(overlap.gameObject);
                }
            }

            return new List<GameObject>(dolls);
        }

        private void OnTriggerEnter(Collider other)
        {
            // 인형이 골 박스(배출구 내부 바닥 트리거)에 들어오면 성공 판정
            if (other.CompareTag(dollTag))
            {
                Debug.Log($"[골인] 인형 감지: {other.gameObject.name}");
                OnDollScored?.Invoke(other.gameObject);
            }
        }
    }
}
