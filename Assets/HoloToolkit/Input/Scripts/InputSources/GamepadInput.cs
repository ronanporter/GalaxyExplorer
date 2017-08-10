﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using UnityEngine;
using UnityEngine.VR.WSA.Input;

namespace GalaxyExplorer.HoloToolkit.Unity.InputModule
{
    public class GamepadInput : GE_Singleton<GamepadInput>
    {
        [Tooltip("Game pad button to press for air tap.")]
        public string GamePadButtonA = "Fire1";

        [Tooltip("Game pad button to press to navigate back.")]
        public string GamePadButtonB = "Go Back";

        [Tooltip("Change this value to give a different source id to your controller.")]
        public uint GamePadId = 50000;

        [Tooltip("Elapsed time for hold started gesture in seconds.")]
        public float HoldStartedInterval = 2.0f;
        [Tooltip("Elapsed time for hold completed gesture in seconds.")]
        public float HoldCompletedInterval = 3.0f;

        [Tooltip("Name of the joystick axis that navigates around X.")]
        public string NavigateAroundXAxisName = "ControllerLeftStickX";

        [Tooltip("Name of the joystick axis that navigates around Y.")]
        public string NavigateAroundYAxisName = "ControllerLeftStickY";

        [Tooltip("Name of the controller thumbstick that rotates the POV 45 degrees")]
        public string GamepadRotationStick = "ControllerRightStickX";

        bool isAPressed = false;
        bool holdStarted = false;
        bool raiseOnce = false;
        bool navigationStarted = false;
        bool navigationCompleted = false;

        enum GestureState
        {
            APressed,
            NavigationStarted,
            NavigationCompleted,
            HoldStarted,
            HoldCompleted,
            HoldCanceled
        }

        GestureState currentGestureState;

        private void Update()
        {
            HandleGamepadAPressed();
            HandleGamepadBPressed();
            HandleRightStickX();
        }

        private bool backButtonPressed = false;
        private void HandleGamepadBPressed()
        {
            if (Input.GetButtonDown(GamePadButtonB))
            {
                backButtonPressed = true;
            }
            if (backButtonPressed && Input.GetButtonUp(GamePadButtonB) && ToolManager.Instance)
            {
                var backButton = ToolManager.Instance.FindButtonByType(ButtonType.Back);
                if (backButton)
                {
                    backButton.ButtonAction();
                }
            }
        }

        public delegate void RotateCameraPovDelegate(float rotationAmount);
        public event RotateCameraPovDelegate RotateCameraPov;

        private float intendedRotation = 0f;

        private void HandleRightStickX()
        {
            float x = Input.GetAxis(GamepadRotationStick);
            if (intendedRotation != 0f && x == 0f)
            {
                RotateCameraPov(intendedRotation);
                intendedRotation = 0f;
            }
            else if (Math.Abs(x) == 1f)
            {
                intendedRotation = 45f * x;
            }
        }

        private void HandleGamepadAPressed()
        {
            if (Input.GetButtonDown(GamePadButtonA))
            {
                //Debug.Log("Gamepad: A pressed");
                isAPressed = true;
                navigationCompleted = false;
                currentGestureState = GestureState.APressed;
                GalaxyExplorer.InputRouter.Instance.PressedSources.Add(InteractionSourceKind.Controller);
            }

            if (isAPressed)
            {
                HandleNavigation();

                if (!holdStarted && !raiseOnce && !navigationStarted)
                {
                    // Raise hold started when user has held A down for certain interval.
                    Invoke("HandleHoldStarted", HoldStartedInterval);
                }

                // Check if we get a subsequent release on A.
                HandleGamepadAReleased();
            }
        }

        private void HandleNavigation()
        {
            if (navigationCompleted)
            {
                return;
            }

            float displacementAlongX = 0.0f;
            float displacementAlongY = 0.0f;

            try
            {
                displacementAlongX = Input.GetAxis(NavigateAroundXAxisName);
                displacementAlongY = Input.GetAxis(NavigateAroundYAxisName);
            }
            catch (Exception)
            {
                Debug.LogWarningFormat("Ensure you have Edit > ProjectSettings > Input > Axes set with values: {0} and {1}",
                    NavigateAroundXAxisName, NavigateAroundYAxisName);
            }

            if (displacementAlongX != 0.0f || displacementAlongY != 0.0f || navigationStarted)
            {
                if (!navigationStarted)
                {
                    //Raise navigation started event.
                    //Debug.Log("GamePad: Navigation started");
                    GalaxyExplorer.InputRouter.Instance.OnNavigationStarted(
                        new NavigationStartedEventArgs(InteractionSourceKind.Controller,
                        Vector3.zero, new HeadPose(), (int)GamePadId));
                    navigationStarted = true;
                    currentGestureState = GestureState.NavigationStarted;
                }

                Vector3 normalizedOffset = new Vector3(displacementAlongX,
                    displacementAlongY,
                    0);

                //Raise navigation updated event.
                //inputManager.RaiseNavigationUpdated(this, GamePadId, normalizedOffset);
                InputRouter.Instance.OnNavigationUpdated(
                    new NavigationUpdatedEventArgs(InteractionSourceKind.Controller,
                    normalizedOffset, new HeadPose(), (int)GamePadId));
            }
        }

        private void HandleGamepadAReleased()
        {
            if (Input.GetButtonUp(GamePadButtonA))
            {
                InputRouter.Instance.PressedSources.Remove(InteractionSourceKind.Controller);

                switch (currentGestureState)
                {
                    case GestureState.NavigationStarted:
                        navigationCompleted = true;
                        CancelInvoke("HandleHoldStarted");
                        CancelInvoke("HandleHoldCompleted");
                        GalaxyExplorer.InputRouter.Instance.OnNavigationCompleted(
                            new NavigationCompletedEventArgs(InteractionSourceKind.Controller,
                            Vector3.zero, new HeadPose(), (int)GamePadId));
                        Reset();
                        break;

                    case GestureState.HoldStarted:
                        CancelInvoke("HandleHoldCompleted");
                        Reset();
                        break;

                    case GestureState.HoldCompleted:
                        Reset();
                        break;

                    default:
                        CancelInvoke("HandleHoldStarted");
                        CancelInvoke("HandleHoldCompleted");
                        InputRouter.Instance.InternalHandleOnTapped();
                        Reset();
                        break;
                }
            }
        }

        private void Reset()
        {
            isAPressed = false;
            holdStarted = false;
            raiseOnce = false;
            navigationStarted = false;
        }

        private void HandleHoldStarted()
        {
            if (raiseOnce || currentGestureState == GestureState.HoldStarted || currentGestureState == GestureState.NavigationStarted)
            {
                return;
            }

            holdStarted = true;
            
            currentGestureState = GestureState.HoldStarted;
            raiseOnce = true;

            Invoke("HandleHoldCompleted", HoldCompletedInterval);
        }

        private void HandleHoldCompleted()
        {
            currentGestureState = GestureState.HoldCompleted;
        }
    }
}