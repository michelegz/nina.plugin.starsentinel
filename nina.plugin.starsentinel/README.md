# Star Sentinel

StarSentinel is a N.I.N.A. plugin that monitors the number of detected stars in your images and helps protect your imaging sessions from degrading sky conditions.

It provides a Loop Condition for the Advanced Sequencer, allowing sequences to automatically continue only while image quality remains within acceptable limits. When a sustained drop in star count is detected, the loop can stop safely, preventing further acquisition of low-quality data.

 StarSentinel analyzes star count trends over time using a moving average approach and configurable thresholds. It is designed to be robust against common astrophotography events such as autofocus runs, filter changes, dithering, and target switches.

This plugin is intended as a lightweight, software-based safeguard, particularly useful when no dedicated sky monitoring hardware (such as an SQM or all-sky camera) is available. It works entirely from image data and integrates seamlessly into existing Advanced Sequencer workflows.

⚠️ Important: StarSentinel is a heuristic and cautionary tool. Star count is not a perfect proxy for sky quality and may be affected by seeing conditions, focus accuracy, filters, or target star density. For fully unattended or critical operations, dedicated hardware solutions are still recommended.