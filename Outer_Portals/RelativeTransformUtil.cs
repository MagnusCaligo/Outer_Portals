using NewHorizons.Utility;
using UnityEngine;

// copied from https://github.com/qsb-dev/quantum-space-buddies/blob/master/QSB/Utility/RelativeTransformUtil.cs
// since its already been tested for 2 years
public static class RelativeTransformUtil
{
	public static Vector3 ToRelPos(this Transform refTransform, Vector3 pos) =>
		refTransform.InverseTransformPoint(pos);

	public static Vector3 FromRelPos(this Transform refTransform, Vector3 relPos) =>
		refTransform.TransformPoint(relPos);

	public static Quaternion ToRelRot(this Transform refTransform, Quaternion rot) =>
		refTransform.InverseTransformRotation(rot);

	public static Quaternion FromRelRot(this Transform refTransform, Quaternion relRot) =>
		refTransform.TransformRotation(relRot);

	// CHANGED
	// we want it relative to a specific transform, not just the body center
	public static Vector3 ToRelVel(this Transform refTransform, Vector3 vel, Vector3 pos) =>
		refTransform.InverseTransformDirection(vel - refTransform.GetAttachedOWRigidbody().GetPointVelocity(pos));

	public static Vector3 FromRelVel(this Transform refTransform, Vector3 relVel, Vector3 pos) =>
		refTransform.GetAttachedOWRigidbody().GetPointVelocity(pos) + refTransform.TransformDirection(relVel);

	public static Vector3 ToRelAngVel(this Transform refTransform, Vector3 angVel) =>
		refTransform.InverseTransformDirection(angVel - refTransform.GetAttachedOWRigidbody().GetAngularVelocity());

	public static Vector3 FromRelAngVel(this Transform refTransform, Vector3 relAngVel) =>
		refTransform.GetAttachedOWRigidbody().GetAngularVelocity() + refTransform.TransformDirection(relAngVel);


	// Process the relative velocities based on the parent body. This is needed because the main body is suspended and the values are not updated.
	public static Vector3 ToRelVelParentBased(this Transform refTransform, Vector3 vel, Vector3 pos) =>
		refTransform.InverseTransformDirection(vel - refTransform.GetAttachedOWRigidbody().GetOrigParentBody().GetPointVelocity(pos));

	public static Vector3 FromRelVelParentBased(this Transform refTransform, Vector3 relVel, Vector3 pos) =>
		refTransform.GetAttachedOWRigidbody().GetOrigParentBody().GetPointVelocity(pos) + refTransform.TransformDirection(relVel);

	public static Vector3 ToRelAngVelParentBased(this Transform refTransform, Vector3 angVel) =>
		refTransform.InverseTransformDirection(angVel - refTransform.GetAttachedOWRigidbody().GetOrigParentBody().GetAngularVelocity());

	public static Vector3 FromRelAngVelParentBased(this Transform refTransform, Vector3 relAngVel) =>
		refTransform.GetAttachedOWRigidbody().GetOrigParentBody().GetAngularVelocity() + refTransform.TransformDirection(relAngVel);
}