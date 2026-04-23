# Star Sentinel

StarSentinel is a N.I.N.A. plugin that monitors the number of detected stars in saved light frames and helps protect imaging sessions from degrading sky conditions.

It provides a Loop Condition for the Advanced Sequencer. The plugin maintains a history of recent star counts, computes the 80th-percentile value as a reference star count, and evaluates each new frame against both a relative threshold and an absolute star count threshold.

A frame is flagged as bad if its relative star count falls below the configured percentile threshold or if the raw star count is below the configured absolute minimum. When too many consecutive bad frames occur, the loop condition becomes false and the sequence can stop safely.

The analysis ignores non-light frames and resets its internal history automatically whenever the imaging context changes — for example when filter, exposure, binning, gain, sensor type, or field-of-view shift changes. This keeps the condition adaptive across targets and exposure settings.

StarSentinel is intended as a lightweight, image-data-based safeguard rather than a replacement for dedicated sky quality hardware. It is heuristic by design and can be affected by focus, filters, target field density, and seeing conditions, so configure thresholds carefully for your setup.

⚠️ Important: StarSentinel is a heuristic and cautionary tool. Star count is not a perfect proxy for sky quality and may be affected by seeing conditions, focus accuracy, filters, or target star density. For fully unattended or critical operations, dedicated hardware solutions are still recommended.