using UnityEngine;

[RequireComponent(typeof(WeaponController))]
public class WeaponSpread : MonoBehaviour
{
    private WeaponController weaponController;
    private float currentSpread;
    private float lastShotTime = -Mathf.Infinity;

    public float CurrentSpread => currentSpread;

    private void Awake()
    {
        weaponController = GetComponent<WeaponController>();
    }

    private void Update()
    {
        if (weaponController == null)
        {
            return;
        }

        GunData gunData = weaponController.CurrentGunData;

        // 连续射击期间不恢复散布
        if (Time.time - lastShotTime < gunData.spreadResetTime)
        {
            return;
        }

        currentSpread = Mathf.MoveTowards(
            currentSpread,
            0f,
            gunData.spreadRecovery * Time.deltaTime
        );
    }

    public float ConsumeSpread()
    {
        if (weaponController == null)
        {
            return 0f;
        }

        GunData gunData = weaponController.CurrentGunData;

        // 距离上一发时间足够长，恢复精准
        if (Time.time - lastShotTime >= gunData.spreadResetTime)
        {
            currentSpread = 0f;
        }

        // 当前这一发使用现有的散布
        float shotSpread = currentSpread;

        // 增加下一发使用的散布
        currentSpread = Mathf.Min(
            currentSpread + gunData.spreadPerShot,
            gunData.maxSpread
        );

        lastShotTime = Time.time;
        return shotSpread;
    }
}