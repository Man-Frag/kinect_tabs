using UnityEngine;

public class IKSelfTest : MonoBehaviour
{
    public Animator animator;

    public Transform rightHandTarget;
    public Transform leftHandTarget;

    [Range(0.0f, 1.0f)]
    public float weight = 1.0f;

    private void Start()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (animator == null)
        {
            Debug.LogError("IKSelfTest: Animator missing.");
            return;
        }

        Debug.Log("IKSelfTest started.");

        if (animator.avatar == null)
        {
            Debug.LogError("IKSelfTest: Animator Avatar is missing.");
        }
        else
        {
            Debug.Log("Avatar name: " + animator.avatar.name);
            Debug.Log("Avatar is human: " + animator.avatar.isHuman);
            Debug.Log("Avatar is valid: " + animator.avatar.isValid);
        }

        if (animator.runtimeAnimatorController == null)
        {
            Debug.LogError("IKSelfTest: Animator Controller is missing.");
        }
        else
        {
            Debug.Log("Animator Controller: " + animator.runtimeAnimatorController.name);
        }
    }

    private void OnAnimatorIK(int layerIndex)
    {
        Debug.Log("IKSelfTest: OnAnimatorIK is running.");

        if (animator == null)
        {
            return;
        }

        if (rightHandTarget != null)
        {
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, weight);
            animator.SetIKPosition(AvatarIKGoal.RightHand, rightHandTarget.position);
        }

        if (leftHandTarget != null)
        {
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, weight);
            animator.SetIKPosition(AvatarIKGoal.LeftHand, leftHandTarget.position);
        }
    }
}