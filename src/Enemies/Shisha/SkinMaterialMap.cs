using UnityEngine;

namespace LethalCompanyShisha.Enemies;

[System.Serializable]
public struct SkinMaterialMap
{
    public ShishaSkinType skinType;

    public Material bodyMaterial;
    public Material hornsMaterial;
}