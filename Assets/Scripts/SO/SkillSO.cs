using System.Collections;
using System.Collections.Generic;
using System.IO.Enumeration;
using UnityEngine;

[CreateAssetMenu(fileName = "NewSkill", menuName = "SkillTree/Skill")]
public class SkillSO : ScriptableObject 
{
    public string skillName;
    public int maxLevel=5;
    public Sprite skillIcon;

}
