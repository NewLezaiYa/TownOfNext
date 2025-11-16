namespace TONX.Roles.Core.Interfaces;

public interface IVoteModifier
{
    /// <summary>
    /// 修改投票的优先级
    /// </summary>
    public int ModifyPriority { get; }
    /// <summary>
    /// 投票结束后修改投票
    /// </summary>
    public void ModifyVoteAfterVoting() { }
}
