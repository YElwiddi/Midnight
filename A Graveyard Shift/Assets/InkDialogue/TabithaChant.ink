VAR player_friendly = 0
VAR player_mean = 0
VAR player_scared = 0
VAR player_smart = 0
VAR player_brave = 0
VAR player_stupid = 0
VAR Sanity = 100
VAR GraveyardProtection = 100

=== start ===
#speaker: Tabitha
Ah! Yes! There it is...
Did you feel that?
They scattered so fast.

The old ones hate being named.
Especially out loud.

+ [You’re finished, right?]
    -> tabitha_excited

=== tabitha_excited ===
#speaker: Tabitha
Finished?
No... completed.

This place will sleep better now.
You helped more than you know.

Thank you, gravekeeper.
For your cooperation.

We may speak again.
~Sanity = Sanity + 35
~GraveyardProtection = GraveyardProtection - 35

-> END