using UnityEngine;
using SIGVerse.RosBridge;
using SIGVerse.Common;
using System.Collections.Generic;
using System;
using static SIGVerse.PR2.PR2Common;

namespace SIGVerse.PR2
{
	public class HPR2SubJointTrajectory : RosSubMessage<SIGVerse.RosBridge.trajectory_msgs.JointTrajectory>, IGraspedObjectHandler
	{
		public class TrajectoryInfo
		{
			public float       StartTime       { get; set; }
			public List<float> Durations       { get; set; }
			public List<float> GoalPositions   { get; set; }
			public float       CurrentTime     { get; set; }
			public float       CurrentPosition { get; set; }

			public TrajectoryInfo(float startTime, List<float> duration, List<float> goalPosition, float currentTime, float currentPosition)
			{
				this.StartTime       = startTime;
				this.Durations       = duration;
				this.GoalPositions   = goalPosition;
				this.CurrentTime     = currentTime;
				this.CurrentPosition = currentPosition;
			}

			public TrajectoryInfo(List<float> duration, List<float> goalPosition, float currentPosition)
			{
				this.StartTime       = Time.time;
				this.Durations       = duration;
				this.GoalPositions   = goalPosition;
				this.CurrentTime     = Time.time;
				this.CurrentPosition = currentPosition;
			}
		}

		private Transform torsoLiftLink;
		private Transform headPanLink;
		private Transform headTiltLink;

		private Transform lShoulderPanLink;
		private Transform lShoulderLiftLink;
		private Transform lUpperArmRollLink;
		private Transform lElbowFlexLink;
		private Transform lForearmRollLink;
		private Transform lWristFlexLink;
		private Transform lWristRollLink;

		private Transform rShoulderPanLink;
		private Transform rShoulderLiftLink;
		private Transform rUpperArmRollLink;
		private Transform rElbowFlexLink;
		private Transform rForearmRollLink;
		private Transform rWristFlexLink;
		private Transform rWristRollLink;

		private float torsoLiftLinkIniPosZ;

		private Dictionary<PR2Common.Joint, TrajectoryInfo> trajectoryInfoMap;
		private List<PR2Common.Joint> trajectoryKeyList;

		private GameObject graspedObject;


		void Awake()
		{
			this.torsoLiftLink     = SIGVerseUtils.FindTransformFromChild(this.transform.root, Link.torso_lift_link.ToString());
			this.headPanLink       = SIGVerseUtils.FindTransformFromChild(this.transform.root, Link.head_pan_link  .ToString());
			this.headTiltLink      = SIGVerseUtils.FindTransformFromChild(this.transform.root, Link.head_tilt_link .ToString());

			this.lShoulderPanLink  = SIGVerseUtils.FindTransformFromChild(this.transform.root, Link.l_shoulder_pan_link  .ToString());
			this.lShoulderLiftLink = SIGVerseUtils.FindTransformFromChild(this.transform.root, Link.l_shoulder_lift_link .ToString());
			this.lUpperArmRollLink = SIGVerseUtils.FindTransformFromChild(this.transform.root, Link.l_upper_arm_roll_link.ToString());
			this.lElbowFlexLink    = SIGVerseUtils.FindTransformFromChild(this.transform.root, Link.l_elbow_flex_link    .ToString());
			this.lForearmRollLink  = SIGVerseUtils.FindTransformFromChild(this.transform.root, Link.l_forearm_roll_link  .ToString());
			this.lWristFlexLink    = SIGVerseUtils.FindTransformFromChild(this.transform.root, Link.l_wrist_flex_link    .ToString());
			this.lWristRollLink    = SIGVerseUtils.FindTransformFromChild(this.transform.root, Link.l_wrist_roll_link    .ToString());

			this.rShoulderPanLink  = SIGVerseUtils.FindTransformFromChild(this.transform.root, Link.r_shoulder_pan_link  .ToString());
			this.rShoulderLiftLink = SIGVerseUtils.FindTransformFromChild(this.transform.root, Link.r_shoulder_lift_link .ToString());
			this.rUpperArmRollLink = SIGVerseUtils.FindTransformFromChild(this.transform.root, Link.r_upper_arm_roll_link.ToString());
			this.rElbowFlexLink    = SIGVerseUtils.FindTransformFromChild(this.transform.root, Link.r_elbow_flex_link    .ToString());
			this.rForearmRollLink  = SIGVerseUtils.FindTransformFromChild(this.transform.root, Link.r_forearm_roll_link  .ToString());
			this.rWristFlexLink    = SIGVerseUtils.FindTransformFromChild(this.transform.root, Link.r_wrist_flex_link    .ToString());
			this.rWristRollLink    = SIGVerseUtils.FindTransformFromChild(this.transform.root, Link.r_wrist_roll_link    .ToString());

			this.torsoLiftLinkIniPosZ = this.torsoLiftLink.localPosition.z;

			this.trajectoryInfoMap = new Dictionary<PR2Common.Joint, TrajectoryInfo>()
			{
				{ PR2Common.Joint.torso_lift_joint,       null },
				{ PR2Common.Joint.head_pan_joint,         null },
				{ PR2Common.Joint.head_tilt_joint,        null },
				{ PR2Common.Joint.l_shoulder_pan_joint,   null },
				{ PR2Common.Joint.l_shoulder_lift_joint,  null },
				{ PR2Common.Joint.l_upper_arm_roll_joint, null },
				{ PR2Common.Joint.l_elbow_flex_joint,     null },
				{ PR2Common.Joint.l_forearm_roll_joint,   null },
				{ PR2Common.Joint.l_wrist_flex_joint,     null },
				{ PR2Common.Joint.l_wrist_roll_joint,     null },
				{ PR2Common.Joint.r_shoulder_pan_joint,   null },
				{ PR2Common.Joint.r_shoulder_lift_joint,  null },
				{ PR2Common.Joint.r_upper_arm_roll_joint, null },
				{ PR2Common.Joint.r_elbow_flex_joint,     null },
				{ PR2Common.Joint.r_forearm_roll_joint,   null },
				{ PR2Common.Joint.r_wrist_flex_joint,     null },
				{ PR2Common.Joint.r_wrist_roll_joint,     null }
			};

			this.trajectoryKeyList = new List<PR2Common.Joint>(trajectoryInfoMap.Keys);
		}


		protected override void Start()
		{
			base.Start();
			
			this.graspedObject = null;
		}


		protected override void SubscribeMessageCallback(SIGVerse.RosBridge.trajectory_msgs.JointTrajectory jointTrajectory)
		{			
			if (this.IsTrajectryMsgCorrect(ref jointTrajectory) == false){ return; }

			this.SetTrajectoryInfoMap(ref jointTrajectory);

			this.CheckOverLimitSpeed();
		}


		protected void FixedUpdate()
		{

			foreach(PR2Common.Joint joint in this.trajectoryKeyList)
			{
				if (this.trajectoryInfoMap[joint] == null){ return; }
				
				switch(joint)
				{
					case PR2Common.Joint.torso_lift_joint:      { this.UpdateLinkPosition(this.torsoLiftLink, joint, Vector3.forward, this.torsoLiftLinkIniPosZ); break; }

					case PR2Common.Joint.head_pan_joint:        { this.UpdateLinkAngle(this.headPanLink,       joint, Vector3.back);   break; }
					case PR2Common.Joint.head_tilt_joint:       { this.UpdateLinkAngle(this.headTiltLink,      joint, Vector3.up);     break; }

					case PR2Common.Joint.l_shoulder_pan_joint:  { this.UpdateLinkAngle(this.lShoulderPanLink,  joint, Vector3.back);   break; }
					case PR2Common.Joint.l_shoulder_lift_joint: { this.UpdateLinkAngle(this.lShoulderLiftLink, joint, Vector3.up);     break; }
					case PR2Common.Joint.l_upper_arm_roll_joint:{ this.UpdateLinkAngle(this.lUpperArmRollLink, joint, Vector3.right);  break; }
					case PR2Common.Joint.l_elbow_flex_joint:    { this.UpdateLinkAngle(this.lElbowFlexLink,    joint, Vector3.up);     break; }
					case PR2Common.Joint.l_forearm_roll_joint:  { this.UpdateLinkAngle(this.lForearmRollLink,  joint, Vector3.right);  break; }
					case PR2Common.Joint.l_wrist_flex_joint:    { this.UpdateLinkAngle(this.lWristFlexLink,    joint, Vector3.up);     break; }
					case PR2Common.Joint.l_wrist_roll_joint:    { this.UpdateLinkAngle(this.lWristRollLink,    joint, Vector3.right);  break; }

					case PR2Common.Joint.r_shoulder_pan_joint:  { this.UpdateLinkAngle(this.rShoulderPanLink,  joint, Vector3.forward);break; }
					case PR2Common.Joint.r_shoulder_lift_joint: { this.UpdateLinkAngle(this.rShoulderLiftLink, joint, Vector3.up);     break; }
					case PR2Common.Joint.r_upper_arm_roll_joint:{ this.UpdateLinkAngle(this.rUpperArmRollLink, joint, Vector3.left);   break; }
					case PR2Common.Joint.r_elbow_flex_joint:    { this.UpdateLinkAngle(this.rElbowFlexLink,    joint, Vector3.up);     break; }
					case PR2Common.Joint.r_forearm_roll_joint:  { this.UpdateLinkAngle(this.rForearmRollLink,  joint, Vector3.right);  break; }
					case PR2Common.Joint.r_wrist_flex_joint:    { this.UpdateLinkAngle(this.rWristFlexLink,    joint, Vector3.up);     break; }
					case PR2Common.Joint.r_wrist_roll_joint:    { this.UpdateLinkAngle(this.rWristRollLink,    joint, Vector3.right);  break; }
				}
			}
		}

		private void UpdateLinkPosition(Transform link, PR2Common.Joint joint, Vector3 axis, float initialPosition)
		{
			float newPos = GetPositionAndUpdateTrajectory(this.trajectoryInfoMap, joint);

			if(Mathf.Abs(axis.x)==1)
			{
				link.localPosition = new Vector3(initialPosition, link.localPosition.y, link.localPosition.z) + newPos * axis;
			}
			else if(Mathf.Abs(axis.y)==1)
			{
				link.localPosition = new Vector3(link.localPosition.x, initialPosition, link.localPosition.z) + newPos * axis;
			}
			else if(Mathf.Abs(axis.z)==1)
			{
				link.localPosition = new Vector3(link.localPosition.x, link.localPosition.y, initialPosition) + newPos * axis;
			}
		}

		private void UpdateLinkAngle(Transform link, PR2Common.Joint joint, Vector3 axis)
		{
			float newPos = PR2Common.GetClampedEulerAngle(GetPositionAndUpdateTrajectory(this.trajectoryInfoMap, joint) * Mathf.Rad2Deg, joint);

			if(Mathf.Abs(axis.x)==1)
			{
				link.localEulerAngles = new Vector3(0.0f, link.localEulerAngles.y, link.localEulerAngles.z) + newPos * axis;
			}
			else if(Mathf.Abs(axis.y)==1)
			{
				link.localEulerAngles = new Vector3(link.localEulerAngles.x, 0.0f, link.localEulerAngles.z) + newPos * axis;
			}
			else if(Mathf.Abs(axis.z)==1)
			{
				link.localEulerAngles = new Vector3(link.localEulerAngles.x, link.localEulerAngles.y, 0.0f) + newPos * axis;
			}
		}

		private static float GetPositionAndUpdateTrajectory(Dictionary<PR2Common.Joint, TrajectoryInfo> trajectoryInfoMap, PR2Common.Joint joint)
		{
			float minSpeed = PR2Common.GetMinJointSpeed(joint);
			float maxSpeed = PR2Common.GetMaxJointSpeed(joint);

			TrajectoryInfo trajectoryInfo = trajectoryInfoMap[joint];

			int targetPointIndex = GetTargetPointIndex(trajectoryInfo);

			float speed = 0.0f;

			if (trajectoryInfo.CurrentTime - trajectoryInfo.StartTime >= trajectoryInfo.Durations[targetPointIndex])
			{
				speed = maxSpeed;
			}
			else
			{
				speed = Mathf.Abs((trajectoryInfo.GoalPositions[targetPointIndex] - trajectoryInfo.CurrentPosition) / (trajectoryInfo.Durations[targetPointIndex] - (trajectoryInfo.CurrentTime - trajectoryInfo.StartTime)));
				speed = Mathf.Clamp(speed, minSpeed, maxSpeed);
			}

			// Calculate position
			float newPosition;
			float movingDistance = speed * (Time.time - trajectoryInfo.CurrentTime);

			if (movingDistance > Mathf.Abs(trajectoryInfo.GoalPositions[targetPointIndex] - trajectoryInfo.CurrentPosition))
			{
				newPosition = trajectoryInfo.GoalPositions[targetPointIndex];
				trajectoryInfoMap[joint] = null;
			}
			else
			{
				trajectoryInfo.CurrentTime = Time.time;

				if (trajectoryInfo.GoalPositions[targetPointIndex] > trajectoryInfo.CurrentPosition)
				{
					trajectoryInfo.CurrentPosition = trajectoryInfo.CurrentPosition + movingDistance;
					newPosition = trajectoryInfo.CurrentPosition;
				}
				else
				{
					trajectoryInfo.CurrentPosition = trajectoryInfo.CurrentPosition - movingDistance;
					newPosition = trajectoryInfo.CurrentPosition;
				}
			}			

			return newPosition;
		}


		private bool IsTrajectryMsgCorrect(ref SIGVerse.RosBridge.trajectory_msgs.JointTrajectory msg)
		{
			for (int i = 0; i < msg.points.Count; i++)
			{
				if (msg.joint_names.Count != msg.points[i].positions.Count) {
					SIGVerseLogger.Warn("Trajectry count error. (joint_names.Count = " + msg.joint_names.Count + ", msg.points[" + i + "].positions.Count = " + msg.points[i].positions.Count);
					return false;
				}
			}
			
			if (msg.joint_names.Count == 1) // Torso
			{
				if (msg.joint_names.Contains(PR2Common.Joint.torso_lift_joint.ToString()))
				{
					return true;
				}
			}
			else if (msg.joint_names.Count == 2) // Head
			{
				if (msg.joint_names.Contains(PR2Common.Joint.head_pan_joint .ToString()) && 
				    msg.joint_names.Contains(PR2Common.Joint.head_tilt_joint.ToString())
				){
					return true;
				}
			}
			else if (msg.joint_names.Count == 7) // Arm
			{
				// Left Arm
				if (msg.joint_names.Contains(PR2Common.Joint.l_shoulder_pan_joint  .ToString()) && 
				    msg.joint_names.Contains(PR2Common.Joint.l_shoulder_lift_joint .ToString()) && 
				    msg.joint_names.Contains(PR2Common.Joint.l_upper_arm_roll_joint.ToString()) && 
				    msg.joint_names.Contains(PR2Common.Joint.l_elbow_flex_joint    .ToString()) && 
				    msg.joint_names.Contains(PR2Common.Joint.l_forearm_roll_joint  .ToString()) && 
				    msg.joint_names.Contains(PR2Common.Joint.l_wrist_flex_joint    .ToString()) && 
				    msg.joint_names.Contains(PR2Common.Joint.l_wrist_roll_joint    .ToString())
				){
					return true;
				}
				// Right Arm
				if (msg.joint_names.Contains(PR2Common.Joint.r_shoulder_pan_joint  .ToString()) && 
				    msg.joint_names.Contains(PR2Common.Joint.r_shoulder_lift_joint .ToString()) && 
				    msg.joint_names.Contains(PR2Common.Joint.r_upper_arm_roll_joint.ToString()) && 
				    msg.joint_names.Contains(PR2Common.Joint.r_elbow_flex_joint    .ToString()) && 
				    msg.joint_names.Contains(PR2Common.Joint.r_forearm_roll_joint  .ToString()) && 
				    msg.joint_names.Contains(PR2Common.Joint.r_wrist_flex_joint    .ToString()) && 
				    msg.joint_names.Contains(PR2Common.Joint.r_wrist_roll_joint    .ToString())
				){
					return true;
				}
			}

			SIGVerseLogger.Warn("Wrong joint name or points. (" + this.topicName + ")");
			return false;
		}

		private void SetTrajectoryInfoMap(ref SIGVerse.RosBridge.trajectory_msgs.JointTrajectory msg)
		{
			for (int i = 0; i < msg.joint_names.Count; i++)
			{
				PR2Common.Joint joint = (PR2Common.Joint)Enum.Parse(typeof(PR2Common.Joint), msg.joint_names[i]);
				
				List<float> positions = new List<float>();
				List<float> durations = new List<float>();
				
				for (int pointIndex = 0; pointIndex < msg.points.Count; pointIndex++)
				{
					positions.Add(PR2Common.GetClampedPosition((float)msg.points[pointIndex].positions[i], joint));
					durations.Add((float)msg.points[pointIndex].time_from_start.secs + (float)msg.points[pointIndex].time_from_start.nsecs * 1.0e-9f);
				}

				switch(joint)
				{
					case PR2Common.Joint.torso_lift_joint: { this.SetJointTrajectoryPosition(joint, durations, positions, this.torsoLiftLink.localPosition.z - this.torsoLiftLinkIniPosZ); break; }

					case PR2Common.Joint.head_pan_joint:        { this.SetJointTrajectoryRotation(joint, durations, positions, -this.headPanLink      .localEulerAngles.z); break; }
					case PR2Common.Joint.head_tilt_joint:       { this.SetJointTrajectoryRotation(joint, durations, positions, +this.headTiltLink     .localEulerAngles.y); break; }

					case PR2Common.Joint.l_shoulder_pan_joint:  { this.SetJointTrajectoryRotation(joint, durations, positions, -this.lShoulderPanLink .localEulerAngles.z); break; }
					case PR2Common.Joint.l_shoulder_lift_joint: { this.SetJointTrajectoryRotation(joint, durations, positions, +this.lShoulderLiftLink.localEulerAngles.y); break; }
					case PR2Common.Joint.l_upper_arm_roll_joint:{ this.SetJointTrajectoryRotation(joint, durations, positions, +this.lUpperArmRollLink.localEulerAngles.x); break; }
					case PR2Common.Joint.l_elbow_flex_joint:    { this.SetJointTrajectoryRotation(joint, durations, positions, +this.lElbowFlexLink   .localEulerAngles.y); break; }
					case PR2Common.Joint.l_forearm_roll_joint:  { this.SetJointTrajectoryRotation(joint, durations, positions, +this.lForearmRollLink .localEulerAngles.x); break; }
					case PR2Common.Joint.l_wrist_flex_joint:    { this.SetJointTrajectoryRotation(joint, durations, positions, +this.lWristFlexLink   .localEulerAngles.y); break; }
					case PR2Common.Joint.l_wrist_roll_joint:    { this.SetJointTrajectoryRotation(joint, durations, positions, +this.lWristRollLink   .localEulerAngles.x); break; }

					case PR2Common.Joint.r_shoulder_pan_joint:  { this.SetJointTrajectoryRotation(joint, durations, positions, +this.rShoulderPanLink .localEulerAngles.z); break; }
					case PR2Common.Joint.r_shoulder_lift_joint: { this.SetJointTrajectoryRotation(joint, durations, positions, +this.rShoulderLiftLink.localEulerAngles.y); break; }
					case PR2Common.Joint.r_upper_arm_roll_joint:{ this.SetJointTrajectoryRotation(joint, durations, positions, -this.rUpperArmRollLink.localEulerAngles.x); break; }
					case PR2Common.Joint.r_elbow_flex_joint:    { this.SetJointTrajectoryRotation(joint, durations, positions, +this.rElbowFlexLink   .localEulerAngles.y); break; }
					case PR2Common.Joint.r_forearm_roll_joint:  { this.SetJointTrajectoryRotation(joint, durations, positions, +this.rForearmRollLink .localEulerAngles.x); break; }
					case PR2Common.Joint.r_wrist_flex_joint:    { this.SetJointTrajectoryRotation(joint, durations, positions, +this.rWristFlexLink   .localEulerAngles.y); break; }
					case PR2Common.Joint.r_wrist_roll_joint:    { this.SetJointTrajectoryRotation(joint, durations, positions, +this.rWristRollLink   .localEulerAngles.x); break; }
				}
			}
		}

		private void SetJointTrajectoryPosition(PR2Common.Joint joint, List<float> durations, List<float> goalPositions, float value)
		{
			this.trajectoryInfoMap[joint] = new TrajectoryInfo(durations, goalPositions, value);
		}

		private void SetJointTrajectoryRotation(PR2Common.Joint joint, List<float> durations, List<float> goalPositions, float value)
		{
			this.trajectoryInfoMap[joint] = new TrajectoryInfo(durations, goalPositions, PR2Common.GetClampedEulerAngle(value, joint) * Mathf.Deg2Rad);
		}

		private void CheckOverLimitSpeed()
		{
			foreach (PR2Common.Joint joint in this.trajectoryKeyList)
			{
				if (this.trajectoryInfoMap[joint] == null) { continue; }

				List<float> trajectoryInfoDurations     = new List<float>(this.trajectoryInfoMap[joint].Durations);
				List<float> trajectoryInfoGoalPositions = new List<float>(this.trajectoryInfoMap[joint].GoalPositions);

				trajectoryInfoDurations.Insert(0, 0.0f);
				trajectoryInfoGoalPositions.Insert(0, this.trajectoryInfoMap[joint].CurrentPosition);

				for (int i = 1; i < trajectoryInfoGoalPositions.Count; i++)
				{
					double tempDistance  = Math.Abs(trajectoryInfoGoalPositions[i] - trajectoryInfoGoalPositions[i-1]);
					double tempDurations = Math.Abs(trajectoryInfoDurations    [i] - trajectoryInfoDurations    [i-1]);
					double tempSpeed     = tempDistance / tempDurations;
					
					if(IsOverLimitSpeed(joint, tempSpeed))
					{
						SIGVerseLogger.Warn("Trajectry speed error. (" + this.topicName + ")");
						return;
					}
				}
			}
		}

		private static bool IsOverLimitSpeed(PR2Common.Joint joint, double speed)
		{
			return speed > PR2Common.GetMaxJointSpeed(joint);
		}

		private static int GetTargetPointIndex(TrajectoryInfo trajectoryInfo)
		{
			int targetPointIndex = 0;

			for (int i = 0; i < trajectoryInfo.Durations.Count; i++)
			{
				targetPointIndex = i;

				if (Time.time - trajectoryInfo.StartTime < trajectoryInfo.Durations[targetPointIndex])
				{
					break;
				}
			}
			return targetPointIndex;
		}


		public void OnChangeGraspedObject(GameObject graspedObject)
		{
			this.graspedObject = graspedObject;
		}
	}
}

