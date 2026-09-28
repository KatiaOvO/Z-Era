using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PrologueController : MonoBehaviour
{
    [Tooltip("序章场景中Glock上的Pickable Item脚本")]
    public PickableItem GlockPickableItem;

    void Start()
    {
        
    }

    void Update()
    {
        Set();
    }

    private void Set()
    {
        // 第一段对话结束后
        if(DialogueFlags.HasFlag("pr_tk_01_unfinished"))
        {
            GlockPickableItem.enabled = true;
        }
    }
}
