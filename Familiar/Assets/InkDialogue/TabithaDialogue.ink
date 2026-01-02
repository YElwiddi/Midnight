EXTERNAL SetEventVar(varName, value)
VAR player_friendly = 0
VAR player_mean = 0
VAR player_scared = 0
VAR player_smart = 0
VAR player_brave = 0
VAR player_stupid = 0

=== start ===
#speaker: Tabitha
Good evening.
You must be the gravekeeper.

My name is Tabitha.
I was hoping you’d be on duty tonight.

+ [I'm not interested.]
    ~player_mean = player_mean + 1
    -> tabitha_direct
+ [Can I help you with something?]
    ~player_friendly = player_friendly + 1
    -> tabitha_friendly

=== tabitha_direct ===
#speaker: Tabitha
Curiosity keeps most people alive longer than confidence.
I ask for a mere moment.

I want to enter the graveyard.
Not to disturb anything.
To protect it.

There are… restless things.
You don’t see them.
But they notice places like this.

+ [That sounds like nonsense.]
    ~player_smart = player_smart + 1
    -> tabitha_dismiss
+ [Protect it how?]
    ~player_scared = player_scared + 1
    -> tabitha_explain

=== tabitha_friendly ===
#speaker: Tabitha
You have a kind voice.
That helps, in a place like this.

I work with old rites.
Small blessings.
They keep bad spirits from wandering too close.

This graveyard feels thin.
Like a door left unlocked.

+ [You expect me to believe that?]
    ~player_smart = player_smart + 1
    -> tabitha_dismiss
+ [What kind of rites?]
    ~player_scared = player_scared + 1
    -> tabitha_explain

=== tabitha_dismiss ===
#speaker: Tabitha
You call it nonsense.
That’s fine.

Most people do.
Until something follows them home.

I don’t charge.
I don’t ask for thanks.
Just permission.

-> tabitha_request

=== tabitha_explain ===
#speaker: Tabitha
Nothing dramatic.
No candles. No blood. Just words said in the right places.

The dead sleep better after, and the living too.

You look tired.
That kind of tired doesn’t come from long shifts.

-> tabitha_request

=== tabitha_request ===
#speaker: Tabitha
Let me inside.
I’ll walk the paths.
Say what needs to be said.
Then I’ll be gone.

You won’t owe me anything.

+ [Alright. Go ahead, but I’m watching you.]
    ~player_brave = player_brave + 1
     ~ SetEventVar("allowed_inside", true)
    -> tabitha_allow
+ [No. I can’t let you in.]
    ~player_mean = player_mean + 1
     ~ SetEventVar("allowed_inside", false)
    -> tabitha_deny

=== tabitha_allow ===
#speaker: Tabitha
Good.
You made the right choice.

If tonight feels quieter than usual, that’s me helping.

-> END

=== tabitha_deny ===
#speaker: Tabitha
I see.
You guard the gate, but not what slips through cracks.

Very well.
Don’t say I didn’t offer.

Sleep lightly tonight.

-> END