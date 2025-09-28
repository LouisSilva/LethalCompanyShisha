using LethalCompanyShisha.Util.Types;
using UnityEngine;

namespace LethalCompanyShisha.Enemies.CustomStateMachineBehaviours;

public class BaseStateMachineBehaviour : StateMachineBehaviour
{
    protected CachedUnityObject<ShishaNetcodeController> netcodeController;

    public void Initialize(ShishaNetcodeController receivedNetcodeController)
    {
        netcodeController.Set(receivedNetcodeController);
    }
}