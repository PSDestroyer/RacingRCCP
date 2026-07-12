//----------------------------------------------
//        Realistic Car Controller Pro
//
// Copyright 2014 - 2025 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Customization loadout.
/// </summary>
[System.Serializable]
public class RCCP_CustomizationLoadout : IRCCP_LoadoutComponent {

    [System.Serializable]
    public struct SerializableColor
    {
        public float r;
        public float g;
        public float b;
        public float a;

        public SerializableColor(float r, float g, float b, float a)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }

        public SerializableColor(Color color)
        {
            r = color.r;
            g = color.g;
            b = color.b;
            a = color.a;
        }

        public Color ToColor()
        {
            return new Color(r, g, b, a);
        }
    }

    public SerializableColor paint = new SerializableColor(1f, 1f, 1f, 0f);
    
    [Min(-1)] public int spoiler = -1;
    [Min(-1)] public int siren = -1;
    [Min(-1)] public int wheel = -1;

    [Min(0)] public int engineLevel = 0;
    [Min(0)] public int handlingLevel = 0;
    [Min(0)] public int brakeLevel = 0;
    [Min(0)] public int speedLevel = 0;

    [Min(-1)] public int decalIndexFront = -1;
    [Min(-1)] public int decalIndexBack = -1;
    [Min(-1)] public int decalIndexLeft = -1;
    [Min(-1)] public int decalIndexRight = -1;

    [Min(-1)] public int neonIndex = -1;

    public List<int> purchasedSpoilers = new List<int>();
    public List<int> purchasedSirens = new List<int>();
    public List<int> purchasedWheels = new List<int>();
    public List<int> purchasedNeons = new List<int>();
    public List<int> purchasedDecalsFront = new List<int>();
    public List<int> purchasedDecalsBack = new List<int>();
    public List<int> purchasedDecalsLeft = new List<int>();
    public List<int> purchasedDecalsRight = new List<int>();

    public RCCP_CustomizationData customizationData = new RCCP_CustomizationData();

    public void EnsurePurchaseState() {

        purchasedSpoilers ??= new List<int>();
        purchasedSirens ??= new List<int>();
        purchasedWheels ??= new List<int>();
        purchasedNeons ??= new List<int>();
        purchasedDecalsFront ??= new List<int>();
        purchasedDecalsBack ??= new List<int>();
        purchasedDecalsLeft ??= new List<int>();
        purchasedDecalsRight ??= new List<int>();

        AddPurchased(purchasedSpoilers, spoiler);
        AddPurchased(purchasedSirens, siren);
        AddPurchased(purchasedWheels, wheel);
        AddPurchased(purchasedNeons, neonIndex);
        AddPurchased(purchasedDecalsFront, decalIndexFront);
        AddPurchased(purchasedDecalsBack, decalIndexBack);
        AddPurchased(purchasedDecalsLeft, decalIndexLeft);
        AddPurchased(purchasedDecalsRight, decalIndexRight);

    }

    public bool IsSpoilerPurchased(int index) {
        EnsurePurchaseState();
        return index >= 0 && purchasedSpoilers.Contains(index);
    }

    public bool IsSirenPurchased(int index) {
        EnsurePurchaseState();
        return index >= 0 && purchasedSirens.Contains(index);
    }

    public bool IsWheelPurchased(int index) {
        EnsurePurchaseState();
        return index >= 0 && purchasedWheels.Contains(index);
    }

    public bool IsNeonPurchased(int index) {
        EnsurePurchaseState();
        return index >= 0 && purchasedNeons.Contains(index);
    }

    public bool IsDecalPurchased(int location, int index) {

        EnsurePurchaseState();

        if (index < 0)
            return false;

        return GetDecalPurchaseList(location).Contains(index);

    }

    public void MarkSpoilerPurchased(int index) {
        EnsurePurchaseState();
        AddPurchased(purchasedSpoilers, index);
    }

    public void MarkSirenPurchased(int index) {
        EnsurePurchaseState();
        AddPurchased(purchasedSirens, index);
    }

    public void MarkWheelPurchased(int index) {
        EnsurePurchaseState();
        AddPurchased(purchasedWheels, index);
    }

    public void MarkNeonPurchased(int index) {
        EnsurePurchaseState();
        AddPurchased(purchasedNeons, index);
    }

    public void MarkDecalPurchased(int location, int index) {

        EnsurePurchaseState();
        AddPurchased(GetDecalPurchaseList(location), index);

    }

    private List<int> GetDecalPurchaseList(int location) {

        return location switch {
            0 => purchasedDecalsFront,
            1 => purchasedDecalsBack,
            2 => purchasedDecalsLeft,
            3 => purchasedDecalsRight,
            _ => purchasedDecalsFront
        };

    }

    private void AddPurchased(List<int> targetList, int index) {

        if (targetList == null || index < 0 || targetList.Contains(index))
            return;

        targetList.Add(index);

    }

    public void UpdateLoadout(MonoBehaviour component) {

        switch (component) {

            case RCCP_VehicleUpgrade_WheelManager:

                RCCP_VehicleUpgrade_WheelManager wheelComponent = (RCCP_VehicleUpgrade_WheelManager)component;
                wheel = wheelComponent.wheelIndex;
                break;

            case RCCP_VehicleUpgrade_UpgradeManager:

                RCCP_VehicleUpgrade_UpgradeManager upgradeComponent = (RCCP_VehicleUpgrade_UpgradeManager)component;
                engineLevel = upgradeComponent.EngineLevel;
                brakeLevel = upgradeComponent.BrakeLevel;
                handlingLevel = upgradeComponent.HandlingLevel;
                speedLevel = upgradeComponent.SpeedLevel;
                break;

            case RCCP_VehicleUpgrade_PaintManager:

                RCCP_VehicleUpgrade_PaintManager paintComponent = (RCCP_VehicleUpgrade_PaintManager)component;
                paint = new SerializableColor(paintComponent.color);
                break;

            case RCCP_VehicleUpgrade_SpoilerManager:

                RCCP_VehicleUpgrade_SpoilerManager spoilerComponent = (RCCP_VehicleUpgrade_SpoilerManager)component;
                spoiler = spoilerComponent.spoilerIndex;
                break;

            case RCCP_VehicleUpgrade_SirenManager:

                RCCP_VehicleUpgrade_SirenManager sirenComponent = (RCCP_VehicleUpgrade_SirenManager)component;
                siren = sirenComponent.sirenIndex;
                break;

            case RCCP_VehicleUpgrade_CustomizationManager:

                RCCP_VehicleUpgrade_CustomizationManager customizationComponent = (RCCP_VehicleUpgrade_CustomizationManager)component;
                customizationData = customizationComponent.customizationData;
                break;

            case RCCP_VehicleUpgrade_DecalManager:

                RCCP_VehicleUpgrade_DecalManager decalManager = (RCCP_VehicleUpgrade_DecalManager)component;
                decalIndexFront = decalManager.index_decalFront;
                decalIndexBack = decalManager.index_decalBack;
                decalIndexLeft = decalManager.index_decalLeft;
                decalIndexRight = decalManager.index_decalRight;
                break;

            case RCCP_VehicleUpgrade_NeonManager:

                RCCP_VehicleUpgrade_NeonManager neonManager = (RCCP_VehicleUpgrade_NeonManager)component;
                neonIndex = neonManager.index;
                break;

        }

    }

}
