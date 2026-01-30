using UnityEngine;

public readonly struct BindResult
{
    public bool Success { get; }
    public BindFailReason FailReason { get; }
    public string Message { get; }
    public MaterialObj Material { get; }

    private BindResult(bool success, BindFailReason failReason, string message, MaterialObj material)
    {
        Success = success;
        FailReason = failReason;
        Message = message;
        Material = material;
    }

    public static BindResult Ok(MaterialObj material) => new(true, BindFailReason.None, null, material);

    public static BindResult Fail(BindFailReason reason, string message) => new(false, reason, message, null);
}


