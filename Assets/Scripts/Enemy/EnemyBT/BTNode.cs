using System.Collections.Generic;

namespace BT
{
    /// <summary>行为树节点状态。</summary>
    public enum NodeStatus
    {
        Inactive,
        Running,
        Success,
        Failure,
    }

    /// <summary>
    /// 行为树节点基类，负责节点生命周期：
    /// 每次 Tick 时，若节点未激活则先 OnEnter，再执行 OnExecute；
    /// 节点结束（Success/Failure）或被打断时会触发 OnExit。
    /// </summary>
    public abstract class BTNode
    {
        public NodeStatus Status { get; private set; } = NodeStatus.Inactive;

        /// <summary>进入节点时调用（首次或重新激活）。</summary>
        protected virtual void OnEnter()
        {
        }

        /// <summary>每帧执行逻辑，返回节点状态。</summary>
        protected abstract NodeStatus OnExecute();

        /// <summary>节点结束或被打断时调用。</summary>
        protected virtual void OnExit()
        {
        }

        public NodeStatus Tick()
        {
            if (Status != NodeStatus.Running)
            {
                OnEnter();
                Status = NodeStatus.Running;
            }

            Status = OnExecute();

            if (Status != NodeStatus.Running)
            {
                OnExit();
            }

            return Status;
        }

        /// <summary>
        /// 重置节点。若节点正在运行（被打断），会触发 OnExit。
        /// </summary>
        public void Reset()
        {
            if (Status == NodeStatus.Running)
            {
                OnExit();
            }

            Status = NodeStatus.Inactive;
        }
    }

    /// <summary>组合节点基类。</summary>
    public abstract class BTComposite : BTNode
    {
        private readonly List<BTNode> children = new List<BTNode>();

        protected BTComposite(params BTNode[] nodes)
        {
            children.AddRange(nodes);
        }

        protected IReadOnlyList<BTNode> Children => children;

        protected void ResetAllChildren()
        {
            for (var i = 0; i < children.Count; i++)
            {
                children[i].Reset();
            }
        }
    }

    /// <summary>
    /// 顺序节点：从左到右依次执行子节点，全部成功才返回 Success；
    /// 任一子节点失败则整体失败并重置。
    /// </summary>
    public class Sequence : BTComposite
    {
        private int currentIndex;

        public Sequence(params BTNode[] nodes) : base(nodes)
        {
        }

        protected override NodeStatus OnExecute()
        {
            while (currentIndex < Children.Count)
            {
                var child = Children[currentIndex];
                var result = child.Tick();

                if (result == NodeStatus.Running)
                {
                    return NodeStatus.Running;
                }

                child.Reset();

                if (result == NodeStatus.Failure)
                {
                    currentIndex = 0;
                    return NodeStatus.Failure;
                }

                currentIndex++;
            }

            currentIndex = 0;
            return NodeStatus.Success;
        }

        protected override void OnExit()
        {
            ResetAllChildren();
            currentIndex = 0;
        }
    }

    /// <summary>
    /// 选择节点：按优先级从左到右执行子节点，任一成功即整体成功；全部失败才失败。
    /// 高优先级子节点激活时，会自动打断（Reset）低优先级仍在运行的子节点。
    /// </summary>
    public class Selector : BTComposite
    {
        private int runningIndex = -1;

        public Selector(params BTNode[] nodes) : base(nodes)
        {
        }

        protected override NodeStatus OnExecute()
        {
            for (var i = 0; i < Children.Count; i++)
            {
                var child = Children[i];
                var result = child.Tick();

                if (result == NodeStatus.Running)
                {
                    // 高优先级子节点（i < runningIndex）接管：打断之前正在运行的低优先级子节点。
                    if (runningIndex >= 0 && runningIndex != i && i < runningIndex)
                    {
                        Children[runningIndex].Reset();
                    }

                    runningIndex = i;
                    return NodeStatus.Running;
                }

                child.Reset();

                if (result == NodeStatus.Success)
                {
                    if (runningIndex >= 0 && runningIndex != i && i < runningIndex)
                    {
                        Children[runningIndex].Reset();
                    }

                    runningIndex = -1;
                    return NodeStatus.Success;
                }
            }

            runningIndex = -1;
            return NodeStatus.Failure;
        }

        protected override void OnExit()
        {
            ResetAllChildren();
            runningIndex = -1;
        }
    }
}
