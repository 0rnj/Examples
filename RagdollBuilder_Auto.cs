//using RootMotion.FinalIK.Demos;
using System;
using System.Collections;
using UnityEngine;

public class RagdollBuilder_Auto : MonoBehaviour
{
    private class BoneInfo
    {
        public string name;

        public Transform anchor;

        public CharacterJoint joint;

        public BoneInfo parent;

        public float minLimit;

        public float maxLimit;

        public float swingLimit;

        public Vector3 axis;

        public Vector3 normalAxis;

        public float radiusScale;

        public Type colliderType;

        public ArrayList children = new ArrayList();

        public float density;

        public float summedMass;
    }

    #region Variables
    private Transform pelvis;

    private Transform leftHips = null;

    private Transform leftKnee = null;

    private Transform leftFoot = null;

    private Transform rightHips = null;

    private Transform rightKnee = null;

    private Transform rightFoot = null;

    private Transform leftArm = null;

    private Transform leftElbow = null;

    private Transform rightArm = null;

    private Transform rightElbow = null;

    private Transform middleSpine = null;

    private Transform head = null;

    public float totalMass = 70f;

    public float strength = 0f;

    private Vector3 right = Vector3.right;

    private Vector3 up = Vector3.up;

    private Vector3 forward = Vector3.forward;

    private Vector3 worldRight = Vector3.right;

    private Vector3 worldUp = Vector3.up;

    private Vector3 worldForward = Vector3.forward;

    public bool flipForward = false;

    private ArrayList bones;

    private BoneInfo rootBone;

    public Player PlayerRef;
    #endregion

    private string CheckConsistency()
    {
        PrepareBones();
        Hashtable hashtable = new Hashtable();
        IEnumerator enumerator = bones.GetEnumerator();
        try
        {
            while (enumerator.MoveNext())
            {
                BoneInfo boneInfo = (BoneInfo)enumerator.Current;
                if ((bool)boneInfo.anchor)
                {
                    if (hashtable[boneInfo.anchor] != null)
                    {
                        BoneInfo boneInfo2 = (BoneInfo)hashtable[boneInfo.anchor];
                        return boneInfo.name + " and " + boneInfo2.name + " may not be assigned to the same bone.";
                    }
                    hashtable[boneInfo.anchor] = boneInfo;
                }
            }
        }
        finally
        {
            IDisposable disposable;
            if ((disposable = (enumerator as IDisposable)) != null)
            {
                disposable.Dispose();
            }
        }
        IEnumerator enumerator2 = bones.GetEnumerator();
        try
        {
            while (enumerator2.MoveNext())
            {
                BoneInfo boneInfo3 = (BoneInfo)enumerator2.Current;
                if ((UnityEngine.Object)boneInfo3.anchor == (UnityEngine.Object)null)
                {
                    return boneInfo3.name + " has not been assigned yet.\n"; // $"{ boneInfo3.name} has not been assigned yet.\n";
                }
            }
        }
        finally
        {
            IDisposable disposable2;
            if ((disposable2 = (enumerator2 as IDisposable)) != null)
            {
                disposable2.Dispose();
            }
        }
        return "";
    }

    private void DecomposeVector(out Vector3 normalCompo, out Vector3 tangentCompo, Vector3 outwardDir, Vector3 outwardNormal)
    {
        outwardNormal = outwardNormal.normalized;
        normalCompo = outwardNormal * Vector3.Dot(outwardDir, outwardNormal);
        tangentCompo = outwardDir - normalCompo;
    }

    private void CalculateAxes()
    {
        if ((UnityEngine.Object)head != (UnityEngine.Object)null && (UnityEngine.Object)pelvis != (UnityEngine.Object)null)
        {
            up = CalculateDirectionAxis(pelvis.InverseTransformPoint(head.position));
        }
        if ((UnityEngine.Object)rightElbow != (UnityEngine.Object)null && (UnityEngine.Object)pelvis != (UnityEngine.Object)null)
        {
            Vector3 _, tangentCompo;
            DecomposeVector(out _, out tangentCompo, pelvis.InverseTransformPoint(rightElbow.position), up);
            right = CalculateDirectionAxis(tangentCompo);
        }
        forward = Vector3.Cross(right, up);
        if (flipForward)
        {
            forward = -forward;
        }
    }

    private void PrepareBones()
    {
        if ((bool)pelvis)
        {
            worldRight = pelvis.TransformDirection(right);
            worldUp = pelvis.TransformDirection(up);
            worldForward = pelvis.TransformDirection(forward);
        }
        bones = new ArrayList();
        rootBone = new BoneInfo();
        rootBone.name = "Pelvis";
        rootBone.anchor = pelvis;
        rootBone.parent = null;
        rootBone.density = 2.5f;
        bones.Add(rootBone);
        AddMirroredJoint("Hips", leftHips, rightHips, "Pelvis", worldRight, worldForward, -20f, 70f, 30f, typeof(CapsuleCollider), 0.3f, 1.5f);
        AddMirroredJoint("Knee", leftKnee, rightKnee, "Hips", worldRight, worldForward, -80f, 0f, 0f, typeof(CapsuleCollider), 0.25f, 1.5f);
        AddJoint("Middle Spine", middleSpine, "Pelvis", worldRight, worldForward, -20f, 20f, 10f, null, 1f, 2.5f);
        AddMirroredJoint("Arm", leftArm, rightArm, "Middle Spine", worldUp, worldForward, -70f, 10f, 50f, typeof(CapsuleCollider), 0.25f, 1f);
        AddMirroredJoint("Elbow", leftElbow, rightElbow, "Arm", worldForward, worldUp, -90f, 0f, 0f, typeof(CapsuleCollider), 0.2f, 1f);
        AddJoint("Head", head, "Middle Spine", worldRight, worldForward, -40f, 25f, 25f, null, 1f, 1f);
    }

    public void BuildRagdoll(GameObject rig)
    {
        PlayerRef = rig.AddComponent<Player>();
        PlayerRef.Colliders = new System.Collections.Generic.List<Collider>();
        PlayerRef.Rigidbodies = new System.Collections.Generic.List<Rigidbody>();
        PlayerRef.PlayerAnimator = rig.GetComponent<Animator>();
        PlayerRef.PowerScale = 1500;
        rig.AddComponent<GenericPoser>();

        int progress = 0;

        // Check body parts' names and assign to key variables
        var parts = rig.GetComponentsInChildren<Transform>();
        string name;
        foreach (var part in parts)
        {
            var ragdollHelper = part.gameObject.AddComponent<RagdollHelper>();
            ragdollHelper.Part = ERagdollPart.NONE;

            // Name formatting
            name = part.gameObject.name.ToLower();
            name.Replace("mixamorig", string.Empty);
            name.Replace("bip", string.Empty);

            // Tag each part
            part.tag = "Player";

            // Pelvis
            if (name.Contains("hips")
                || name.Contains("pelvis"))
            {
                if (pelvis != null) continue;
                pelvis = part;
                ragdollHelper.Part = ERagdollPart.HIP;
                ragdollHelper.AchievemntType = EAchievementType.DAMAGE_HIPS;
                progress++;
            }

            // Left leg
            else
            if (name.Contains("leftupperleg")
                || name.Contains("leftupleg")
                || name.Contains("lefthip")
                || (name.Contains("thigh") && name.Contains("l")))
            {
                if (leftHips != null) continue;
                leftHips = part;
                ragdollHelper.Part = ERagdollPart.LEG;
                ragdollHelper.AchievemntType = EAchievementType.DAMAGE_LEG;
                progress++;
            }
            else
            if ((name.Contains("calf") && name.Contains("l ")) // Space after "l" char for not to be found in "calf"
                || name.Contains("leftleg"))
            {
                if (leftKnee != null) continue;
                leftKnee = part;
                var rr = part.gameObject.AddComponent<RagdollRedirect>();
                rr.RedirectRagdoll = leftHips.GetComponent<RagdollHelper>();
                progress++;
            }
            else
            if ((name.Contains("foot") && name.Contains("l"))
                || name.Contains("leftfoot"))
            {
                if (leftFoot != null) continue;
                leftFoot = part;
                var rr = part.gameObject.AddComponent<RagdollRedirect>();
                rr.RedirectRagdoll = leftHips.GetComponent<RagdollHelper>();
                progress++;
            }

            // Right leg
            else
            if (name.Contains("rightupperleg")
                || name.Contains("rightupleg")
                || name.Contains("righthip")
                || (name.Contains("thigh") && name.Contains("r")))
            {
                if (rightHips != null) continue;
                rightHips = part;
                ragdollHelper.Part = ERagdollPart.LEG;
                ragdollHelper.AchievemntType = EAchievementType.DAMAGE_LEG;
                progress++;
            }
            else
            if ((name.Contains("calf") && name.Contains(" r "))
                || name.Contains("rightleg"))
            {
                if (rightKnee != null) continue;
                rightKnee = part;
                var rr = part.gameObject.AddComponent<RagdollRedirect>();
                rr.RedirectRagdoll = rightHips.GetComponent<RagdollHelper>();
                progress++;
            }
            else
            if ((name.Contains("foot") && name.Contains("r"))
                || name.Contains("rightfoot"))
            {
                if (rightFoot != null) continue;
                rightFoot = part;
                var rr = part.gameObject.AddComponent<RagdollRedirect>();
                rr.RedirectRagdoll = rightHips.GetComponent<RagdollHelper>();
                progress++;
            }

            // Left arm
            else
            if (name.Contains("leftarm")
                || (name.Contains("upperarm") && name.Contains("l")))
            {
                if (leftArm != null) continue;
                leftArm = part;
                ragdollHelper.Part = ERagdollPart.HAND;
                ragdollHelper.AchievemntType = EAchievementType.DAMAGE_HAND;
                progress++;
            }
            else
            if (name.Contains("leftforearm")
                || (name.Contains("forearm") && name.Contains("l")))
            {
                if (leftElbow != null) continue;
                leftElbow = part;
                var rr = part.gameObject.AddComponent<RagdollRedirect>();
                rr.RedirectRagdoll = leftArm.GetComponent<RagdollHelper>();
                progress++;
            }

            // Right arm
            else
            if (name.Contains("rightarm")
                || (name.Contains("upperarm") && name.Contains("r")))
            {
                if (rightArm != null) continue;
                rightArm = part;
                ragdollHelper.Part = ERagdollPart.HAND;
                ragdollHelper.AchievemntType = EAchievementType.DAMAGE_HAND;
                progress++;
            }
            else
            if (name.Contains("rightforearm")
                || (name.Contains("forearm") && name.Contains("r")))
            {
                if (rightElbow != null) continue;
                rightElbow = part;
                var rr = part.gameObject.AddComponent<RagdollRedirect>();
                rr.RedirectRagdoll = rightArm.GetComponent<RagdollHelper>();
                progress++;
            }

            // Spine
            else
            if (name.Contains("spine2")
                || name.Contains("spine1")
                || name.Contains("spine"))
            {
                if (middleSpine != null) continue;
                middleSpine = part;
                ragdollHelper.Part = ERagdollPart.TORS;
                ragdollHelper.AchievemntType = EAchievementType.DAMAGE_TORS;
                progress++;
            }

            // Head
            else
            if (name.Contains("head"))
            {
                if (head != null) continue;
                head = part;
                ragdollHelper.Part = ERagdollPart.HEAD;
                ragdollHelper.AchievemntType = EAchievementType.DAMAGE_HEAD;
                progress++;
            }
        }

        // Removing empty RagdollHelper
        foreach (var part in parts)
        {
            var rh = part.gameObject.GetComponent<RagdollHelper>();
            if (rh.Part == ERagdollPart.NONE)
                DestroyImmediate(rh);
        }

        CheckConsistency();
        CalculateAxes();
        Build();

        DestroyImmediate(this);
    }

    private void Build()
    {
        Cleanup();
        BuildCapsules();
        AddBreastColliders();
        AddHeadCollider();
        BuildBodies();
        BuildJoints();
        CalculateMass();
    }

    private BoneInfo FindBone(string name)
    {
        IEnumerator enumerator = bones.GetEnumerator();
        try
        {
            while (enumerator.MoveNext())
            {
                BoneInfo boneInfo = (BoneInfo)enumerator.Current;
                if (boneInfo.name == name)
                {
                    return boneInfo;
                }
            }
        }
        finally
        {
            IDisposable disposable;
            if ((disposable = (enumerator as IDisposable)) != null)
            {
                disposable.Dispose();
            }
        }
        return null;
    }

    private void AddMirroredJoint(string name, Transform leftAnchor, Transform rightAnchor, string parent, Vector3 worldTwistAxis, Vector3 worldSwingAxis, float minLimit, float maxLimit, float swingLimit, Type colliderType, float radiusScale, float density)
    {
        AddJoint("Left " + name, leftAnchor, parent, worldTwistAxis, worldSwingAxis, minLimit, maxLimit, swingLimit, colliderType, radiusScale, density);
        AddJoint("Right " + name, rightAnchor, parent, worldTwistAxis, worldSwingAxis, minLimit, maxLimit, swingLimit, colliderType, radiusScale, density);
    }

    private void AddJoint(string name, Transform anchor, string parent, Vector3 worldTwistAxis, Vector3 worldSwingAxis, float minLimit, float maxLimit, float swingLimit, Type colliderType, float radiusScale, float density)
    {
        BoneInfo boneInfo = new BoneInfo();
        boneInfo.name = name;
        boneInfo.anchor = anchor;
        boneInfo.axis = worldTwistAxis;
        boneInfo.normalAxis = worldSwingAxis;
        boneInfo.minLimit = minLimit;
        boneInfo.maxLimit = maxLimit;
        boneInfo.swingLimit = swingLimit;
        boneInfo.density = density;
        boneInfo.colliderType = colliderType;
        boneInfo.radiusScale = radiusScale;
        if (FindBone(parent) != null)
        {
            boneInfo.parent = FindBone(parent);
        }
        else if (name.StartsWith("Left"))
        {
            boneInfo.parent = FindBone("Left " + parent);
        }
        else if (name.StartsWith("Right"))
        {
            boneInfo.parent = FindBone("Right " + parent);
        }
        boneInfo.parent.children.Add(boneInfo);
        bones.Add(boneInfo);
    }

    private void BuildCapsules()
    {
        IEnumerator enumerator = bones.GetEnumerator();
        try
        {
            while (enumerator.MoveNext())
            {
                BoneInfo boneInfo = (BoneInfo)enumerator.Current;
                if (boneInfo.colliderType == typeof(CapsuleCollider))
                {
                    int direction;
                    float distance;
                    if (boneInfo.children.Count == 1)
                    {
                        BoneInfo boneInfo2 = (BoneInfo)boneInfo.children[0];
                        Vector3 position = boneInfo2.anchor.position;
                        CalculateDirection(boneInfo.anchor.InverseTransformPoint(position), out direction, out distance);
                    }
                    else
                    {
                        Vector3 position2 = boneInfo.anchor.position - boneInfo.parent.anchor.position + boneInfo.anchor.position;
                        CalculateDirection(boneInfo.anchor.InverseTransformPoint(position2), out direction, out distance);
                        if (boneInfo.anchor.GetComponentsInChildren(typeof(Transform)).Length > 1)
                        {
                            Bounds bounds = default(Bounds);
                            Component[] componentsInChildren = boneInfo.anchor.GetComponentsInChildren(typeof(Transform));
                            for (int i = 0; i < componentsInChildren.Length; i++)
                            {
                                Transform transform = (Transform)componentsInChildren[i];
                                bounds.Encapsulate(boneInfo.anchor.InverseTransformPoint(transform.position));
                            }
                            distance = ((!(distance > 0f)) ? bounds.min[direction] : bounds.max[direction]);
                        }
                    }
                    CapsuleCollider capsuleCollider = boneInfo.anchor.gameObject.AddComponent<CapsuleCollider>();
                    capsuleCollider.direction = direction;
                    Vector3 zero = Vector3.zero;
                    zero[direction] = distance * 0.5f;
                    capsuleCollider.center = zero;
                    capsuleCollider.height = Mathf.Abs(distance);
                    capsuleCollider.radius = Mathf.Abs(distance * boneInfo.radiusScale);
                    PlayerRef.Colliders.Add(capsuleCollider);
                }
            }
        }
        finally
        {
            IDisposable disposable;
            if ((disposable = (enumerator as IDisposable)) != null)
            {
                disposable.Dispose();
            }
        }
    }

    private void Cleanup()
    {
        IEnumerator enumerator = bones.GetEnumerator();
        try
        {
            while (enumerator.MoveNext())
            {
                BoneInfo boneInfo = (BoneInfo)enumerator.Current;
                if ((bool)boneInfo.anchor)
                {
                    Component[] componentsInChildren = boneInfo.anchor.GetComponentsInChildren(typeof(Joint));
                    Component[] array = componentsInChildren;
                    for (int i = 0; i < array.Length; i++)
                    {
                        Joint obj = (Joint)array[i];
                        UnityEngine.Object.DestroyImmediate(obj);
                    }
                    Component[] componentsInChildren2 = boneInfo.anchor.GetComponentsInChildren(typeof(Rigidbody));
                    Component[] array2 = componentsInChildren2;
                    for (int j = 0; j < array2.Length; j++)
                    {
                        Rigidbody obj2 = (Rigidbody)array2[j];
                        UnityEngine.Object.DestroyImmediate(obj2);
                    }
                    Component[] componentsInChildren3 = boneInfo.anchor.GetComponentsInChildren(typeof(Collider));
                    Component[] array3 = componentsInChildren3;
                    for (int k = 0; k < array3.Length; k++)
                    {
                        Collider obj3 = (Collider)array3[k];
                        UnityEngine.Object.DestroyImmediate(obj3);
                    }
                }
            }
        }
        finally
        {
            IDisposable disposable;
            if ((disposable = (enumerator as IDisposable)) != null)
            {
                disposable.Dispose();
            }
        }
    }

    private void BuildBodies()
    {
        IEnumerator enumerator = bones.GetEnumerator();
        try
        {
            while (enumerator.MoveNext())
            {
                BoneInfo boneInfo = (BoneInfo)enumerator.Current;
                PlayerRef.Rigidbodies.Add(boneInfo.anchor.gameObject.AddComponent<Rigidbody>());
                boneInfo.anchor.GetComponent<Rigidbody>().mass = boneInfo.density;
            }
        }
        finally
        {
            IDisposable disposable;
            if ((disposable = (enumerator as IDisposable)) != null)
            {
                disposable.Dispose();
            }
        }
    }

    private void BuildJoints()
    {
        IEnumerator enumerator = bones.GetEnumerator();
        try
        {
            while (enumerator.MoveNext())
            {
                BoneInfo boneInfo = (BoneInfo)enumerator.Current;
                if (boneInfo.parent != null)
                {
                    CharacterJoint characterJoint = boneInfo.joint = boneInfo.anchor.gameObject.AddComponent<CharacterJoint>();
                    characterJoint.axis = CalculateDirectionAxis(boneInfo.anchor.InverseTransformDirection(boneInfo.axis));
                    characterJoint.swingAxis = CalculateDirectionAxis(boneInfo.anchor.InverseTransformDirection(boneInfo.normalAxis));
                    characterJoint.anchor = Vector3.zero;
                    characterJoint.connectedBody = boneInfo.parent.anchor.GetComponent<Rigidbody>();
                    characterJoint.enablePreprocessing = false;
                    SoftJointLimit softJointLimit = default(SoftJointLimit);
                    softJointLimit.contactDistance = 0f;
                    softJointLimit.limit = boneInfo.minLimit;
                    characterJoint.lowTwistLimit = softJointLimit;
                    softJointLimit.limit = boneInfo.maxLimit;
                    characterJoint.highTwistLimit = softJointLimit;
                    softJointLimit.limit = boneInfo.swingLimit;
                    characterJoint.swing1Limit = softJointLimit;
                    softJointLimit.limit = 0f;
                    characterJoint.swing2Limit = softJointLimit;
                }
            }
        }
        finally
        {
            IDisposable disposable;
            if ((disposable = (enumerator as IDisposable)) != null)
            {
                disposable.Dispose();
            }
        }
    }

    private void CalculateMassRecurse(BoneInfo bone)
    {
        float num = bone.anchor.GetComponent<Rigidbody>().mass;
        IEnumerator enumerator = bone.children.GetEnumerator();
        try
        {
            while (enumerator.MoveNext())
            {
                BoneInfo boneInfo = (BoneInfo)enumerator.Current;
                CalculateMassRecurse(boneInfo);
                num += boneInfo.summedMass;
            }
        }
        finally
        {
            IDisposable disposable;
            if ((disposable = (enumerator as IDisposable)) != null)
            {
                disposable.Dispose();
            }
        }
        bone.summedMass = num;
    }

    private void CalculateMass()
    {
        CalculateMassRecurse(rootBone);
        float num = totalMass / rootBone.summedMass;
        IEnumerator enumerator = bones.GetEnumerator();
        try
        {
            while (enumerator.MoveNext())
            {
                BoneInfo boneInfo = (BoneInfo)enumerator.Current;
                boneInfo.anchor.GetComponent<Rigidbody>().mass *= num;
            }
        }
        finally
        {
            IDisposable disposable;
            if ((disposable = (enumerator as IDisposable)) != null)
            {
                disposable.Dispose();
            }
        }
        CalculateMassRecurse(rootBone);
    }

    private static void CalculateDirection(Vector3 point, out int direction, out float distance)
    {
        direction = 0;
        if (Mathf.Abs(point[1]) > Mathf.Abs(point[0]))
        {
            direction = 1;
        }
        if (Mathf.Abs(point[2]) > Mathf.Abs(point[direction]))
        {
            direction = 2;
        }
        distance = point[direction];
    }

    private static Vector3 CalculateDirectionAxis(Vector3 point)
    {
        int direction = 0;
        float distance;
        CalculateDirection(point, out direction, out distance);
        Vector3 zero = Vector3.zero;
        if (distance > 0f)
        {
            zero[direction] = 1f;
        }
        else
        {
            zero[direction] = -1f;
        }
        return zero;
    }

    private static int SmallestComponent(Vector3 point)
    {
        int num = 0;
        if (Mathf.Abs(point[1]) < Mathf.Abs(point[0]))
        {
            num = 1;
        }
        if (Mathf.Abs(point[2]) < Mathf.Abs(point[num]))
        {
            num = 2;
        }
        return num;
    }

    private static int LargestComponent(Vector3 point)
    {
        int num = 0;
        if (Mathf.Abs(point[1]) > Mathf.Abs(point[0]))
        {
            num = 1;
        }
        if (Mathf.Abs(point[2]) > Mathf.Abs(point[num]))
        {
            num = 2;
        }
        return num;
    }

    private static int SecondLargestComponent(Vector3 point)
    {
        int num = SmallestComponent(point);
        int num2 = LargestComponent(point);
        if (num < num2)
        {
            int num3 = num2;
            num2 = num;
            num = num3;
        }
        if (num == 0 && num2 == 1)
        {
            return 2;
        }
        if (num == 0 && num2 == 2)
        {
            return 1;
        }
        return 0;
    }

    private Bounds Clip(Bounds bounds, Transform relativeTo, Transform clipTransform, bool below)
    {
        int index = LargestComponent(bounds.size);
        if (Vector3.Dot(worldUp, relativeTo.TransformPoint(bounds.max)) > Vector3.Dot(worldUp, relativeTo.TransformPoint(bounds.min)) == below)
        {
            Vector3 min = bounds.min;
            min[index] = relativeTo.InverseTransformPoint(clipTransform.position)[index];
            bounds.min = min;
        }
        else
        {
            Vector3 max = bounds.max;
            max[index] = relativeTo.InverseTransformPoint(clipTransform.position)[index];
            bounds.max = max;
        }
        return bounds;
    }

    private Bounds GetBreastBounds(Transform relativeTo)
    {
        Bounds result = default(Bounds);
        result.Encapsulate(relativeTo.InverseTransformPoint(leftHips.position));
        result.Encapsulate(relativeTo.InverseTransformPoint(rightHips.position));
        result.Encapsulate(relativeTo.InverseTransformPoint(leftArm.position));
        result.Encapsulate(relativeTo.InverseTransformPoint(rightArm.position));
        Vector3 size = result.size;
        size[SmallestComponent(result.size)] = size[LargestComponent(result.size)] / 2f;
        result.size = size;
        return result;
    }

    private void AddBreastColliders()
    {
        if ((UnityEngine.Object)middleSpine != (UnityEngine.Object)null && (UnityEngine.Object)pelvis != (UnityEngine.Object)null)
        {
            Bounds bounds = Clip(GetBreastBounds(pelvis), pelvis, middleSpine, false);
            BoxCollider boxCollider = pelvis.gameObject.AddComponent<BoxCollider>();
            boxCollider.center = bounds.center;
            boxCollider.size = bounds.size;
            bounds = Clip(GetBreastBounds(middleSpine), middleSpine, middleSpine, true);
            PlayerRef.Colliders.Add(boxCollider);

            boxCollider = middleSpine.gameObject.AddComponent<BoxCollider>();
            boxCollider.center = bounds.center;
            boxCollider.size = bounds.size;
            PlayerRef.Colliders.Add(boxCollider);
        }
        else
        {
            Bounds bounds2 = default(Bounds);
            bounds2.Encapsulate(pelvis.InverseTransformPoint(leftHips.position));
            bounds2.Encapsulate(pelvis.InverseTransformPoint(rightHips.position));
            bounds2.Encapsulate(pelvis.InverseTransformPoint(leftArm.position));
            bounds2.Encapsulate(pelvis.InverseTransformPoint(rightArm.position));
            Vector3 size = bounds2.size;
            size[SmallestComponent(bounds2.size)] = size[LargestComponent(bounds2.size)] / 2f;
            BoxCollider boxCollider2 = pelvis.gameObject.AddComponent<BoxCollider>();
            boxCollider2.center = bounds2.center;
            boxCollider2.size = size;
            PlayerRef.Colliders.Add(boxCollider2);
        }
    }

    private void AddHeadCollider()
    {
        if ((bool)head.GetComponent<Collider>())
        {
            UnityEngine.Object.Destroy(head.GetComponent<Collider>());
        }

        // Radius calculation
        float num = Vector3.Distance(leftArm.transform.position, rightArm.transform.position);
        num /= 1.5f; // 1.5-2f for big head, 4f for normal

        var parent = head.parent;
        float maxScale = 1;
        while (parent != null)
        {
            var s = parent.localScale.x;
            if (s > maxScale) maxScale = s;
            parent = parent.parent;
        }

        SphereCollider sphereCollider = head.gameObject.AddComponent<SphereCollider>();
        sphereCollider.radius = num / maxScale;

        // Center calculation
        Vector3 zero = Vector3.zero;
        int direction;
        float distance;
        CalculateDirection(head.InverseTransformPoint(pelvis.position), out direction, out distance);
        if (distance > 0f)
        {
            zero[direction] = 0f - num;
        }
        else
        {
            zero[direction] = num;
        }
        zero[direction] /= maxScale;
        sphereCollider.center = zero;
        PlayerRef.Colliders.Add(sphereCollider);
    }
}
