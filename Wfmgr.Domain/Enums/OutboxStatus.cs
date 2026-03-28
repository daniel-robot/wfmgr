namespace Wfmgr.Domain.Enums;

public enum OutboxStatus
{
    New,
    Sent,
    Failed,
    Retrying
}
