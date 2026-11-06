/*
 * Project: Hap-Assist Firmware (Final Release)
 * Author: SeongHyeok Yoon (Dongguk Univ.)
 * Date: 2025-12-19
 *
 * [Description]
 * This firmware implements a 'Dual-Mode Haptic Feedback' system with safety features:
 * 1. Touch Mode (A-E): Short vibration feedback for individual finger collisions.
 * 2. Grabbing Mode (G/R): Rhythmic 'Heartbeat' pulse pattern (1s interval).
 * 3. Watchdog Timer: Auto-shutoff failsafe if signal is lost for >0.2s.
 *
 * [Hardware]
 * - MCU: Arduino Uno (ATmega328P)
 * - Actuators: 5x Coin Vibration Motors
 * - Protocol: Serial (9600 baud), 1-Byte Command
 */

// --- Pin Configuration ---
const int PIN_THUMB  = 2; // Thumb (A)
const int PIN_INDEX  = 7; // Index (B)
const int PIN_MIDDLE = 4; // Middle (C)
const int PIN_RING   = 5; // Ring (D)
const int PIN_PINKY  = 6; // Pinky (E)

// --- Variables for Grabbing Mode (Heartbeat Logic) ---
bool isGrabbing = false;          // Current grabbing state flag
unsigned long lastPulseTime = 0;  // Timestamp for heartbeat rhythm
bool pulseState = false;          // Toggle state for pulse (On/Off)
const long INTERVAL = 1000;       // Pulse Interval (1000ms = 1s)

// --- Watchdog Timer Variables (Safety) ---
unsigned long lastSignalTime = 0; // Timestamp of the last received signal
const long TIMEOUT = 200;         // Failsafe limit: 200ms (0.2s)

void setup() {
  // 1. Initialize Serial Communication (Must match Unity Baudrate)
  Serial.begin(9600);

  // 2. Set Pin Modes
  pinMode(PIN_THUMB, OUTPUT);
  pinMode(PIN_INDEX, OUTPUT);
  pinMode(PIN_MIDDLE, OUTPUT);
  pinMode(PIN_RING, OUTPUT);
  pinMode(PIN_PINKY, OUTPUT);

  // 3. System Ready Signal (Vibrate all for 0.5s)
  AllHigh();
  delay(500);
  AllLow();

  // Initialize Watchdog timer
  lastSignalTime = millis();
}

void loop() {
  // ==========================================
  // [1] Signal Reception & Processing
  // ==========================================
  if (Serial.available() > 0) {
    char signal = Serial.read();

    // Watchdog Reset: Update the last signal timestamp
    lastSignalTime = millis();

    if (signal == 'G') {
      // Start Grabbing Mode (Enable Heartbeat)
      isGrabbing = true;
      pulseState = true;
      lastPulseTime = millis();
      AllHigh(); // Initial feedback
    }
    else if (signal == 'R') {
      // Release Mode (Stop All)
      isGrabbing = false;
      pulseState = false;
      AllLow();
    }
    // Individual Touch Feedback (Only active when NOT grabbing)
    else if (!isGrabbing) {
      switch (signal) {
        case 'A': Vibrate(PIN_THUMB, 100);  break; // Thumb (100ms)
        case 'B': Vibrate(PIN_INDEX, 100);  break; // Index
        case 'C': Vibrate(PIN_MIDDLE, 100); break; // Middle
        case 'D': Vibrate(PIN_RING, 100);   break; // Ring
        case 'E': Vibrate(PIN_PINKY, 100);  break; // Pinky
      }
      // Since Vibrate() uses delay(100), update timestamp again for safety
      lastSignalTime = millis();
    }
  }

  // ==========================================
  // [2] Watchdog Failsafe Check
  // ==========================================
  // If no signal is received for more than 0.2s (TIMEOUT),
  // immediately shut down all motors to prevent overheating or errors.
  if (millis() - lastSignalTime > TIMEOUT) {
    isGrabbing = false;
    pulseState = false;
    AllLow(); // EMERGENCY STOP
  }

  // ==========================================
  // [3] Heartbeat Logic (Grabbing Mode)
  // ==========================================
  if (isGrabbing) {
    // Run only if the connection is alive (Within Watchdog limit)
    if (millis() - lastSignalTime <= TIMEOUT) {
      unsigned long currentTime = millis();

      // Toggle vibration every 1 second
      if (currentTime - lastPulseTime >= INTERVAL) {
        lastPulseTime = currentTime;
        pulseState = !pulseState; // Toggle State
        if (pulseState) AllHigh();
        else AllLow();
      }
    }
  }
}

// --- Helper Functions ---

// Function to vibrate a specific pin for a duration
void Vibrate(int pin, int time) {
  digitalWrite(pin, HIGH);
  delay(time);
  digitalWrite(pin, LOW);
}

// Turn ON all motors
void AllHigh() {
  digitalWrite(PIN_THUMB, HIGH);
  digitalWrite(PIN_INDEX, HIGH);
  digitalWrite(PIN_MIDDLE, HIGH);
  digitalWrite(PIN_RING, HIGH);
  digitalWrite(PIN_PINKY, HIGH);
}

// Turn OFF all motors
void AllLow() {
  digitalWrite(PIN_THUMB, LOW);
  digitalWrite(PIN_INDEX, LOW);
  digitalWrite(PIN_MIDDLE, LOW);
  digitalWrite(PIN_RING, LOW);
  digitalWrite(PIN_PINKY, LOW);
}
