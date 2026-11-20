using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Wuxia
{
    internal sealed class HumanoidMotion : MonoBehaviour
    {
        private PlayableGraph graph;
        private AnimationMixerPlayable mixer;
        private AnimationClipPlayable walk;
        private Transform spine, leftLeg, rightLeg, leftFoot, rightFoot, leftArm, rightArm;
        private Transform rigRoot, leftToe, rightToe;
        private Vector3 fixedRoot, leftHeelSole, rightHeelSole, leftToeSole, rightToeSole;
        private float weight, idleSleeveClearance;
        internal float WalkingSpeed { get; set; }

        internal void Initialize(Animator animator, AnimationClip idle, AnimationClip walking, bool elder, float phase, float ground, float idleSleeveClearance)
        {
            this.idleSleeveClearance = idleSleeveClearance;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (elder) spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            leftLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            rightLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            leftArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            rightArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            rigRoot = animator.transform;
            fixedRoot = rigRoot.localPosition;
            leftToe = animator.GetBoneTransform(HumanBodyBones.LeftToes);
            rightToe = animator.GetBoneTransform(HumanBodyBones.RightToes);
            float floor = rigRoot.TransformPoint(Vector3.up * ground).y;
            leftHeelSole = Sole(leftFoot, floor);
            rightHeelSole = Sole(rightFoot, floor);
            leftToeSole = Sole(leftToe, floor);
            rightToeSole = Sole(rightToe, floor);
            graph = PlayableGraph.Create("Humanoid locomotion");
            mixer = AnimationMixerPlayable.Create(graph, 2);
            var stand = AnimationClipPlayable.Create(graph, idle);
            walk = AnimationClipPlayable.Create(graph, walking);
            stand.SetTime(idle.length * phase);
            walk.SetTime(walking.length * phase);
            graph.Connect(stand, 0, mixer, 0);
            graph.Connect(walk, 0, mixer, 1);
            mixer.SetInputWeight(0, 1);
            var output = AnimationPlayableOutput.Create(graph, "Humanoid", animator);
            output.SetSourcePlayable(mixer);
            graph.Play();
            graph.Evaluate(0);
        }

        private void Update()
        {
            if (!graph.IsValid()) return;
            weight = Mathf.MoveTowards(weight, WalkingSpeed > 0 ? 1 : 0, Time.deltaTime * 6);
            walk.SetSpeed(Mathf.Clamp(WalkingSpeed / 1.55f, .65f, 1.55f));
            mixer.SetInputWeight(0, 1 - weight);
            mixer.SetInputWeight(1, weight);
        }

        private void LateUpdate()
        {
            if (spine != null) spine.rotation = Quaternion.AngleAxis(5, transform.right) * spine.rotation;
            // Narrow the source idle stance to fit the long skirt.
            if (leftLeg == null || rightLeg == null || leftFoot == null || rightFoot == null) return;
            float angle = (1 - weight) * 7;
            Vector3 forward = transform.forward;
            float leftSign = Vector3.Dot(leftLeg.position - rightLeg.position, transform.right) > 0 ? -1 : 1;
            Quaternion l = leftFoot.rotation, r = rightFoot.rotation;
            leftLeg.rotation = Quaternion.AngleAxis(angle * leftSign, forward) * leftLeg.rotation;
            rightLeg.rotation = Quaternion.AngleAxis(-angle * leftSign, forward) * rightLeg.rotation;
            leftFoot.rotation = l;
            rightFoot.rotation = r;
            float sleeveClearance = Mathf.Lerp(idleSleeveClearance, 1.5f, weight) * leftSign;
            if (leftArm != null) leftArm.rotation = Quaternion.AngleAxis(-sleeveClearance, forward) * leftArm.rotation;
            if (rightArm != null) rightArm.rotation = Quaternion.AngleAxis(sleeveClearance, forward) * rightArm.rotation;
            GroundFeet();
        }

        private static Vector3 Sole(Transform bone, float floor)
        {
            if (bone == null) return Vector3.zero;
            Vector3 p = bone.position;
            p.y = floor;
            return bone.InverseTransformPoint(p);
        }

        private void GroundFeet()
        {
            rigRoot.localPosition = fixedRoot;
            float min = Mathf.Min(leftFoot.TransformPoint(leftHeelSole).y, rightFoot.TransformPoint(rightHeelSole).y);
            if (leftToe != null) min = Mathf.Min(min, leftToe.TransformPoint(leftToeSole).y);
            if (rightToe != null) min = Mathf.Min(min, rightToe.TransformPoint(rightToeSole).y);
            float offset = Mathf.Clamp((transform.position.y - min) / transform.lossyScale.y, -.17f, .08f);
            rigRoot.localPosition = fixedRoot + Vector3.up * offset;
        }

        private void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy();
        }
    }
}
