using LethalCompanyShisha.Types;
using UnityEngine;

namespace LethalCompanyShisha.CustomStateMachineBehaviours;

public class BaseStateMachineBehaviour : StateMachineBehaviour
{
    protected CachedUnityObject<ShishaNetcodeController> netcodeController;

    public void Initialize(ShishaNetcodeController receivedNetcodeController)
    {
        netcodeController.Set(receivedNetcodeController);
    }
}