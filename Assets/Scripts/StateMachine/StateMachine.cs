using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Tools;

namespace FSM
{
        public abstract class StateMachine
        {

            //这是一个继承了BindableProperty类型的IState字段，获取Istate要通过.Value
            public GenericProperty<IState> currentState = new GenericProperty<IState>();
            public bool IsRunning { get; private set; } = true;
            public int Revision { get; private set; }

            public void Stop()
            {
                if (!IsRunning) return;
                IsRunning = false;
                Revision++;
                currentState.Value?.Exit();
            }

            public void Start(IState initialState)
            {
                if (initialState == null) throw new ArgumentNullException(nameof(initialState));
                Stop();
                IsRunning = true;
                Revision++;
                currentState.Value = initialState;
                currentState.Value?.Enter();
            }

            /// <summary>
            /// 切换状态的接口API
            /// </summary>
            /// <param name="newState"></param>
            public void ChangeState(IState newState)
            { 
                if (!IsRunning || newState == null || ReferenceEquals(currentState.Value, newState)) return;
                Revision++;
               
                //可能为空用？逻辑符
                currentState.Value?.Exit();

                currentState.Value = newState;

                currentState.Value?.Enter();
            }

            /// <summary>
            /// 处理输入的接口API
            /// </summary>
            public void HandInput()
            {
                //只允许一个状态在这里更新
                if (IsRunning) currentState.Value?.HandInput();
            }
            /// <summary>
            /// 更新非物理逻辑的接口API
            /// </summary>
            public void Update()
            {
                if (IsRunning) currentState.Value?.Update();
            }
            /// <summary>
            /// 执行动画事件的接口API
            /// </summary>
            public void OnAnimationTranslateEvent(IState translateState)
            {
                if (IsRunning) currentState.Value?.OnAnimationTranslateEvent(translateState);
            }
            
            public void OnAnimationExitEvent() 
            {
                if (IsRunning) currentState.Value?.OnAnimationExitEvent();
            }
        }
}
