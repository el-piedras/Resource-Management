﻿using System;
using System.Collections;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Messaging;
using System.Threading;
using HarmonyLib;
using JetBrains.Annotations;
using OWML.Common;
using OWML.ModHelper;
using Steamworks;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;

namespace project1;

public class project1 : ModBehaviour
{
	//	Variables list & referencing objects
	public static Transform timberHearth_Transform;
	public OWTriggerVolume landPadTriggerVolume;
	public static bool IsLanded;
	public static bool isOnLandingPad;
	public static project1 Instance;
	public static ShipResources shipResources;
	public static ShipFuelGauge shipFuelGauge;
	public static ShipOxygenGauge shipOxygenGauge;
	public static OxygenDetector shipOxygenDetector;
	public static OxygenDetector playerOxygenDetector;
	public static OxygenVolume shipOxygenVolume;
	public static PlayerState playerState;
	public static PlayerResources playerResources;
	public static PlayerRecoveryPoint playerRecoveryPoint;
	public static GameObject playerDetector;
	public static ElectricalSystem mainElectricalSystem;
	public static LandingPadManager landingPadManager;
	public static Locator locator;
	static public NotificationData criticalFuelNotification;
	static public NotificationData lowOxygenNotification;
	static public NotificationData criticalOxygenNotification;
	static public NotificationData oxygenDepleted;
	static public ShipThrusterModel shipThrusterModel;
	static public ThrusterModel thrusterModel;
	static public bool isSeatedExitDelay;
	static public bool isPlayerSeated = PlayerState.AtFlightConsole();
	public static bool isUsingEmergencyThrusters;

	// Settings
	// Oxygen settings
	public static float oxygenRefillAmount;
	public static float maxOxygen;
	public static bool enableOxygenRegen;

	// Fuel settings
	public static float refuelAmount;
	public static float maxFuel;
	public static float landingPadRefuelMultiplier;
	public static bool enableRefuel;

	// Misc settings
	public static bool useOxygenAsFuel;
	public static float oxygenAsFuelMultiplier;
	private void Awake()
	{
		Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
		Instance = this;
	}	

	private void Start()
	{
		ModHelper.Console.WriteLine($"{nameof(project1)}: Resource management loaded!", MessageType.Success);

		// Check the scene loaded when a scene finishes loading
		LoadManager.OnCompleteSceneLoad += (scene, loadScene) =>
		{
			//	Checks if the scene is SolarSystem
			if (loadScene == OWScene.SolarSystem)
			{
				//	Getting objects for references:
				GameObject playerBody = GameObject.Find("Player_Body");
				if (playerBody != null)
				{
					playerResources = playerBody.GetComponent<PlayerResources>();
				}

				GameObject modelRocket_Body = GameObject.Find("ModelRocket_Body");
				if (modelRocket_Body != null)
				{
					thrusterModel = modelRocket_Body.GetComponent<ThrusterModel>();
				}

				GameObject shipBody = GameObject.Find("Ship_Body");
				if (shipBody != null)
				{
					shipResources = shipBody.GetComponent<ShipResources>();
					landingPadManager = shipBody.GetComponent<LandingPadManager>();
					shipThrusterModel = shipBody.GetComponent<ShipThrusterModel>();

					// Sets the max oxygen to the desired value. It drains 1unit/s
					shipResources._maxOxygen = maxOxygen;
					shipResources._currentOxygen = maxOxygen;

					// Sets the max fuel (For customizing purposes, it uses the vanilla value by default)
					shipResources._maxFuel = maxFuel;
					shipResources._currentFuel = maxFuel;
				}

				GameObject timberHearth_Body = GameObject.Find("TimberHearth_Body");
				if (timberHearth_Body != null)
				{
					timberHearth_Transform = timberHearth_Body.GetComponent<Transform>();
					
					// Create object for collider stuff
					GameObject THLandPadColliderObj = new GameObject("THLandPadColliderObj");
					THLandPadColliderObj.transform.parent = timberHearth_Transform;
					THLandPadColliderObj.transform.localPosition = new Vector3(-16f, -51.7f, 223.5f);

					SphereShape THLandPadColliderShape = THLandPadColliderObj.AddComponent<SphereShape>();
					THLandPadColliderShape.radius = 12f;
					THLandPadColliderShape._pointChecksOnly = true;

					landPadTriggerVolume = THLandPadColliderObj.AddComponent<OWTriggerVolume>();
					landPadTriggerVolume._active = true;
					landPadTriggerVolume._shape = THLandPadColliderShape;

					if (landPadTriggerVolume != null)
					{
						landPadTriggerVolume.OnEntry += OnEntry;
						landPadTriggerVolume.OnExit += OnExit;
					}
				}

				GameObject globalManagers = GameObject.Find("GlobalManagers");
				if (globalManagers != null)
				{
					playerState = globalManagers.GetComponent<PlayerState>();
					locator = globalManagers.GetComponent<Locator>();
					if (locator != null)
					{
						GameObject shipDetector = GameObject.Find("ShipDetector");
						if (shipDetector != null)
						{
							shipOxygenDetector = shipDetector.AddComponent<OxygenDetector>();
						}
					}
				}

				GameObject gaugeSystems = GameObject.Find("GaugeSystems");
				if (gaugeSystems != null)
				{
					shipOxygenGauge = gaugeSystems.GetComponent<ShipOxygenGauge>();
					shipFuelGauge = gaugeSystems.GetComponent<ShipFuelGauge>();
				}

				playerDetector = GameObject.Find("PlayerDetector");
				if (playerDetector != null)
				{
					playerOxygenDetector = playerDetector.GetComponent<OxygenDetector>();
				}

				GameObject shipAtmosphereVolume = GameObject.Find("ShipAtmosphereVolume");
				if (shipAtmosphereVolume != null)
				{
					shipOxygenVolume = shipAtmosphereVolume.GetComponent<OxygenVolume>();
				}

				GameObject playerRecoveryPointObject = GameObject.Find("PlayerRecoveryPoint");
				if (playerRecoveryPointObject != null)
				{
					playerRecoveryPoint = playerRecoveryPointObject.GetComponent<PlayerRecoveryPoint>();
				}

				GameObject mainElectricitySystemObject = GameObject.Find("MainElectricalSystem");
				if (mainElectricitySystemObject != null)
				{
					mainElectricalSystem = mainElectricitySystemObject.GetComponent<ElectricalSystem>();
				}

				// Notification Display Data
				shipResources._fuelDepletedNotification = null;
				shipResources._fuelDepletedNotification = new NotificationData(NotificationTarget.Ship, "FUEL DEPLETED. USING OXYGEN AS FUEL", 1f, true);
				// lowFuelNotification = new NotificationData(NotificationTarget.Ship, "WARNING: LOW FUEL", 1f, true); // Unused for now
				criticalFuelNotification = new NotificationData(NotificationTarget.Ship, "WARNING: FUEL LEVEL CRITICAL", 1f, true);
				lowOxygenNotification = new NotificationData(NotificationTarget.Ship, "WARNING: LOW OXYGEN", 1f, true);
				criticalOxygenNotification = new NotificationData(NotificationTarget.Ship, "WARNING: OXYGEN LEVEL CRITICAL", 1f, true);
				oxygenDepleted = new NotificationData(NotificationTarget.Ship, "OXYGEN DEPLETED", 1f, true);
				
			}
		};
	}
	public override void Configure(IModConfig config) 
	{
	//	// Oxygen settings
		maxOxygen = config.GetSettingsValue<float>("Max Oxygen");
		oxygenRefillAmount = config.GetSettingsValue<float>("Oxygen Regeneration Rate");
		enableOxygenRegen = config.GetSettingsValue<bool>("Enable Oxygen Refill");

		// Fuel settings
		maxFuel = config.GetSettingsValue<float>("Max Fuel");
		refuelAmount = config.GetSettingsValue<float>("Refuel Amount");
		landingPadRefuelMultiplier = config.GetSettingsValue<float>("Landing Pad Refuel Multiplier");
		enableRefuel = config.GetSettingsValue<bool>("Enable Refuel");

		// Misc settings
		useOxygenAsFuel = config.GetSettingsValue<bool>("Use Oxygen as Fuel");
		oxygenAsFuelMultiplier = config.GetSettingsValue<float>("Oxygen as Fuel Multiplier");
	}
	private void FixedUpdate()
	{
		// 	Function calls
		if (shipResources != null)
		{
			Functions.FuelManagement();
			Functions.OxygenManagement();
			Functions.OxygenAsThrusters();
		}
	}
	private void OnEntry(GameObject collidedObject)
	{
		var body = collidedObject.GetAttachedOWRigidbody();
		if (!body.CompareTag("Ship")) return;

		isOnLandingPad = true;
	}

	private void OnExit(GameObject collidedObject)
	{
			var body = collidedObject.GetAttachedOWRigidbody();
			if (body.CompareTag("Ship") && collidedObject != null)
			{
				isOnLandingPad = false;
			}
	}

	public class Functions
	{
		static public void FuelManagement()
		{
			landingPadManager.IsLanded(); // Call's the landing check to update boolean (Failsafe)
			float fractionaryFuel = shipResources.GetFractionalFuel(); // Get ship's fractional fuel
			IsLanded = landingPadManager.IsLanded();    // Checks if ship is landed


			// Refueling and gauges manager
			if (!shipResources._hullBreach)
			{
				// Refueling
				if (enableRefuel && IsLanded)
				{
					if (isOnLandingPad)
					{
						shipResources.SetFuel(shipResources._currentFuel += refuelAmount * landingPadRefuelMultiplier); // Multiply refuel if landed on landing pad
						shipFuelGauge._indicatorLight.SetEmissionColor(Color.magenta);
					}
					else
					{
						shipResources.SetFuel(shipResources._currentFuel += refuelAmount);  // Increase fuel when landed
						shipFuelGauge._indicatorLight.SetEmissionColor(Color.green);    //	Turn gauge green light on when refueling
					}
				}

				// Gauges
				// No fuel status
				if (!IsLanded && fractionaryFuel > 0.3f)
				{
					shipFuelGauge._indicatorLight.SetEmissionColor(Color.black); // Turn light off when not refueling
				}

				// Critical fuel level
				else if (0f < fractionaryFuel && fractionaryFuel <= 0.3f && !IsLanded)
				{
					shipFuelGauge._indicatorLight.SetEmissionColor(Color.red); // Turn on warning light when low fuel
				}

				// Ship's notification manager
				// No status
				if (fractionaryFuel > 0.3)
				{
					if (NotificationManager.SharedInstance.IsPinnedNotification(criticalFuelNotification))
					{
						NotificationManager.SharedInstance.UnpinNotification(criticalFuelNotification);
					}
				}

				// Fuel level critical
				else if (0f < fractionaryFuel && fractionaryFuel <= 0.3f)
				{
					if (!NotificationManager.SharedInstance.IsPinnedNotification(criticalFuelNotification))
					{
						NotificationManager.SharedInstance.PostNotification(criticalFuelNotification, true);
					}
				}

				// Fuel depleted
				else if (fractionaryFuel == 0)
				{
					if (NotificationManager.SharedInstance.IsPinnedNotification(criticalFuelNotification))
					{
						NotificationManager.SharedInstance.UnpinNotification(criticalFuelNotification);
					}
				}

				// Electrical systems & Refill station manager
				if (shipResources._currentFuel <= 0f || shipResources._hullBreach)
				{
					playerRecoveryPoint._refuelsPlayer = false; // Disables refueling when ship is out of fuel
					mainElectricalSystem.SetPowered(false); // Disables electrical systems when ship is out of fuel
				}
				else if (shipResources._currentFuel > 0f && !shipResources._hullBreach)
				{
					playerRecoveryPoint._refuelsPlayer = true;  // Activates refueling station in case of refueling
					mainElectricalSystem.SetPowered(true);  // Enables electrical system in case of refueling
				}

				// Draw fuel to refill jetpack
				bool isRefueling = playerResources.IsRefueling();
				if (isRefueling && PlayerState.IsInsideShip() && shipResources._currentFuel > 30f)
				{
					shipResources._currentFuel -= 30f;
				}
			}
		}
		static public void OxygenManagement()
		{
			bool isOxygenAvailable = shipOxygenDetector.GetDetectOxygen();  // Check oxygen availabilty
			bool playerHasOxygenVolume = playerOxygenDetector._activeVolumes.Contains(shipOxygenVolume);    // Check if player has ship's OxygenVolume
			float fractionaryOxygen = shipResources.GetFractionalOxygen();  // Get fractional oxygen
			bool isPlayerInShip = PlayerState.IsInsideShip();   // Check if player is inside ship

			// Oxygen regeneration
			if (enableOxygenRegen)
			{
				// Refilling oxygen
				if (isOxygenAvailable && enableOxygenRegen)
				{
					shipResources.SetOxygen(shipResources._currentOxygen += oxygenRefillAmount);
					shipOxygenGauge._indicatorLight.SetEmissionColor(Color.green);
				}
			}
			
			// Gauges
			// No oxygen status
			if (!isOxygenAvailable && fractionaryOxygen > 0.33f)
			{
				shipOxygenGauge._indicatorLight.SetEmissionColor(Color.black); // Turn light off when oxygen is not available
			}

			// Low oxygen
			else if (0.16f < fractionaryOxygen && fractionaryOxygen <= 0.33f && !isOxygenAvailable)
			{
				shipOxygenGauge._indicatorLight.SetEmissionColor(Color.yellow);
			}

			// Critical oxygen
			else if (0f < fractionaryOxygen && fractionaryOxygen <= 0.16f && !isOxygenAvailable)
			{
				shipOxygenGauge._indicatorLight.SetEmissionColor(Color.red);    // Turn red light on critical oxygen
			}

			// Notifications manager
			// No condition:
			if (fractionaryOxygen > 0.33f && NotificationManager.SharedInstance.IsPinnedNotification(lowOxygenNotification))
			{
				NotificationManager.SharedInstance.UnpinNotification(lowOxygenNotification);
			}

			// Low oxygen: 
			else if (!NotificationManager.SharedInstance.IsPinnedNotification(lowOxygenNotification) && 0.16f < fractionaryOxygen && fractionaryOxygen <= 0.33f)
			{
				NotificationManager.SharedInstance.PostNotification(lowOxygenNotification, true);
				NotificationManager.SharedInstance.UnpinNotification(criticalOxygenNotification);
			}

			// Critical condition:
			else if (!NotificationManager.SharedInstance.IsPinnedNotification(criticalOxygenNotification) && 0f < fractionaryOxygen && fractionaryOxygen <= 0.16f)
			{
				NotificationManager.SharedInstance.PostNotification(criticalOxygenNotification, true);
				NotificationManager.SharedInstance.UnpinNotification(lowOxygenNotification);
				NotificationManager.SharedInstance.UnpinNotification(oxygenDepleted);
			}

			// Oxygen depleted
			else if (!NotificationManager.SharedInstance.IsPinnedNotification(oxygenDepleted) && fractionaryOxygen == 0f)
			{
				NotificationManager.SharedInstance.PostNotification(oxygenDepleted, true);
				NotificationManager.SharedInstance.UnpinNotification(criticalOxygenNotification);
			}
			
			//Consume supplies upon refill
			bool isPlayerSeated = PlayerState.AtFlightConsole();

			if (isPlayerInShip && !isPlayerSeated && playerResources._currentOxygen < PlayerResources._maxOxygen && shipResources._currentOxygen > 0f)
			{
				shipResources._currentOxygen -= 1f;
				playerResources._currentOxygen += 1f;
			}

			//Ship takes player's oxygen when the player sits (and has enough available storage)
			if (isPlayerSeated && playerResources._currentOxygen > 0f && shipResources._currentOxygen < shipResources._maxOxygen - PlayerResources._maxOxygen)
			{
				shipResources._currentOxygen += 1f;
				playerResources._currentOxygen -= 1f;
			}

			if (shipResources._currentOxygen == 0f && playerHasOxygenVolume && isPlayerInShip || !isPlayerInShip)
			{
				playerOxygenDetector._activeVolumes.Remove(shipOxygenVolume); 
			}

			// Add ship's OxygenVolume to player in case of refill
			else if (shipResources._currentOxygen > 0 && !playerHasOxygenVolume && isPlayerInShip)
			{
				playerOxygenDetector._activeVolumes.Add(shipOxygenVolume);
			}
		}
		static public void OxygenAsThrusters()
		{
			if (useOxygenAsFuel)
			{
				// Reduce oxygen when thrusting if fuel = 0
				if (shipResources._currentFuel == 0f)
				{
					float magnitude = shipThrusterModel.GetLocalAcceleration().magnitude;
					if (!isUsingEmergencyThrusters)
					{
						isUsingEmergencyThrusters = true;
						NotificationManager.SharedInstance.PostNotification(shipResources._fuelDepletedNotification);
					}
					if (!shipOxygenDetector.GetDetectOxygen())
					{
						if (shipResources._currentOxygen > 0f)
						{
							float oxygenDecrease = magnitude * oxygenAsFuelMultiplier * Time.deltaTime;
							shipResources._currentOxygen -= oxygenDecrease;
						}
					}
				}
				else
				{
					isUsingEmergencyThrusters = false;
				}
				if (!isUsingEmergencyThrusters && NotificationManager.SharedInstance.IsPinnedNotification(shipResources._fuelDepletedNotification))
				{
					NotificationManager.SharedInstance.UnpinNotification(shipResources._fuelDepletedNotification);
				}
			}
		}
	}
	public IEnumerator ExitFlightConsoleDelay()
	{
		yield return new WaitForSeconds(8);
		if (!PlayerState.AtFlightConsole())
		{
			isSeatedExitDelay = false;
		}
	}
	
	//	Create Harmony patches class
	[HarmonyPatch]
	public class HarmonyPatches
	{
		// Override if thrusters are usable when out of fuel
		[HarmonyPostfix]
		[HarmonyPatch(typeof(ShipResources),nameof(ShipResources.AreThrustersUsable))]
		public static void AreThrustersUsable_Postfix(ref bool __result, ShipResources __instance)
		{
			if (useOxygenAsFuel)
			{
				bool isOxygenAboveZero = __instance._currentOxygen > 0f;
				bool isFuelAboveZero = __instance._currentFuel > 0f;
				__result = isFuelAboveZero || isOxygenAboveZero;
			}
		}

		// Create a patch to modify the amount of fuel used
		[HarmonyPrefix]
		[HarmonyPatch(typeof(ShipResources), nameof(ShipResources.DrainFuel))]
		public static void ShipResources_DrainFuel_Prefix(ref float amount)
		{
			// Calculate the increased fuel usage
			amount *= 7f;
		}

		// Create a patch to increase oxygen consumption on the ship
		[HarmonyPrefix]
		[HarmonyPatch(typeof(ShipResources), nameof(ShipResources.DrainOxygen))]
		public static void ShipResources_DrainOxygen_Prefix(ref float amount)
		{
			//	Calculate increased oxygen drain
			bool isOxygenAvailable = shipOxygenDetector.GetDetectOxygen();
			if (isOxygenAvailable)
			{
				amount *= 0f;
			}
			else
			{
				// Makes it so the oxygen drain is 1f/s (600f will last 10 minutes :p)
				amount = 1 * Time.deltaTime;
			}
		}

		// Create a patch to disable oxygen function when necessary
		[HarmonyPrefix]
		[HarmonyPatch(typeof(PlayerResources), nameof(PlayerResources.UpdateOxygen))]
		public static bool PlayerResources_UpdateOxygen_Prefix()
		{
			if (playerResources._currentOxygen == 0f && shipResources._currentOxygen == 0f && isPlayerSeated)
			{
				return true;
			}
			else if (isSeatedExitDelay && shipResources._currentOxygen != 0f && playerResources._currentOxygen != 0f && !shipResources._hullBreach)
			{
				return false;
			}
			else if (!isSeatedExitDelay)
			{
				return true;
			}
			return true;
		}

		// Get when player enters flight console
		[HarmonyPostfix]
		[HarmonyPatch(typeof(PlayerState), nameof(PlayerState.OnEnterFlightConsole))]
		public static void PlayerState_OnEnterFlightConsole_Postfix()
		{
			isSeatedExitDelay = true;
		}

		// Get when player exits flight console
		[HarmonyPostfix]
		[HarmonyPatch(typeof(PlayerState), nameof(PlayerState.OnExitFlightConsole))]
		public static void PlayerState_OnExitFlightConsole_Postfix()
		{
			Instance.StartCoroutine(Instance.ExitFlightConsoleDelay());
		}
	}
}