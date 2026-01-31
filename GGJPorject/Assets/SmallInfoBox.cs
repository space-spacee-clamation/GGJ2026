using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class SmallInfoBox : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI value;

    /// <summary>
    /// 使用字符串刷新显示值。
    /// </summary>
    public void Refresh(string text)
    {
        if (value != null)
        {
            value.text = text ?? string.Empty;
        }
    }
}
