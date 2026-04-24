using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Tools
{
    public class MathToos
    {
        /// 无影响Lerp
        public static float UnTetheredLerp(float time = 10f)
        {
            return 1 - Mathf.Exp(-time * Time.deltaTime);
        }
        
        /// 取目标方向向量
        public static Vector3 DirectionForTarget(Transform target, Transform self)
        {
            return (self.position - target.position).normalized;
        }
        
        /// 返回目标之间的距离
        public static float DistanceForTarget(Transform target, Transform self)
        {
            return Vector3.Distance(self.position, target.position);
        }
        
        /// 获取增量角
        public static float GetDeltaAngle(Transform currentDirection, Vector3 targetDirection)
        {
            //当前角色朝向的角度
            //不完全等同于欧拉角的y，因为单纯的欧拉角在斜坡并不是我们想要的
            float angleCurrent = Mathf.Atan2(currentDirection.forward.x, currentDirection.forward.z) * Mathf.Rad2Deg;
            //目标方向的角度也就是希望角色转过去的那个方向的角度
            float targetAngle = Mathf.Atan2(targetDirection.x, targetDirection.z) * Mathf.Rad2Deg;

            return Mathf.DeltaAngle(angleCurrent, targetAngle);
        }


        /// 计算当前朝向于目标方向之间的夹角
        public static float GetAngleForTargetDirection(Transform target, Transform self)
        {
            return Vector3.Angle(((self.position - target.position).normalized), self.forward);
        }
        
        /// 将角度限制在指定范围内（支持循环角度，如 [-180, 180] 或 [0, 360]）
        public static float ClampAngle(float angle, float min, float max)
        {
            float range = max - min;
    
            // 归一化角度
            angle = (angle - min) % range;
            if (angle < 0)
                angle += range;
    
            // 映射
            return angle + min;
        }
        
        /// 从当前位置移动到目标位置
        public static Vector3 TargetPositionOffset(Transform target, Transform self, float time)
        {
            var pos = target.transform.position;
            return Vector3.MoveTowards(self.position, pos, UnTetheredLerp(time));
        }

        /// <summary>
        /// 打印日志
        /// </summary>
        /// <param name="message"></param>
        public static void WTF(object message)
        {
            Debug.LogFormat($"日志内容:<color=#ff0000> --->   {message}   <--- </color>");
        }
    }
}