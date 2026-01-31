EXTERNAL SetEventVar(varName, value)
VAR player_mean = 0
VAR player_scared = 0
VAR player_stupid = 0
VAR SpiritAngered = 0
VAR GraveRobberSetup = 0

=== start ===
#speaker: ??? 
Oh.
Good. You’re still here.

I was worried I’d be too late.

You’re the gravekeeper, yes?

+ [Who are you?]
    -> sister_intro
+ [The graveyard is closed.]
    ~player_mean = player_mean + 1
    -> sister_closed

=== sister_intro ===
#speaker: Maribel
My name is Maribel.

I’m looking for someone who came through here earlier.
A woman named Tabitha.

Thin. Quiet. Thinks she knows how the dead work.

She’s my sister.

+ [She was here earlier.]
    -> sister_after_tabitha
+ [No one like that came by.]
    -> sister_no_tabitha

=== sister_closed ===
#speaker: Maribel
Closed.
Yes, I heard that already.

But you see, I’m not here for the graveyard.

I’m here because my sister has a habit of interfering with things she doesn’t understand.

And this place?
It attracts those things.

+ [Interfering how?]
    ~player_scared = player_scared + 1
    -> sister_explain
+ [I still can’t let you in.]
    -> sister_request

=== sister_after_tabitha ===
#speaker: Maribel
I knew it.
She never could resist places like this.

Always trying to “fix” things.
Always making them worse.

If she performed her little rites here, then something of mine is now trapped inside.

That’s why I need to enter.

+ [Something of yours?]
    ~player_scared = player_scared + 1
    -> sister_explain
+ [That’s not my problem.]
    ~player_mean = player_mean + 1
    -> sister_request

=== sister_no_tabitha ===
#speaker: Maribel
Interesting.

Then she plans to come later.
Or she sent me ahead.

Either way, the result is the same.

There’s something buried here that doesn’t belong to the dead.
And I can feel it breathing.

+ [Breathing?]
    ~player_scared = player_scared + 1
    -> sister_explain
+ [You’re not making this better.]
    -> sister_request

=== sister_explain ===
#speaker: Maribel
Tabitha believes in keeping doors closed.

I believe in knowing what’s on the other side.

When she meddles, she binds things halfway.
Not alive. Not gone.

If I don’t retrieve what she disturbed, it will start calling out.
First to the dead, then to you.

I won’t need long.

-> sister_request

=== sister_request ===
#speaker: Maribel
Let me inside.

I’ll find what my sister tangled up.
I’ll take it with me.
And this place will finally be quiet.

If you refuse…
Well.
It will still get out eventually.

+ [Fine. Go in. But be quick.]
     ~ SetEventVar("allowed_inside", true)
    -> sister_allow
+ [No. You’re not setting foot inside.]
     ~player_mean = player_mean + 1
     ~ SetEventVar("allowed_inside", false)
    -> sister_deny

=== sister_allow ===
#speaker: Maribel
Thank you.

You’re smarter than my sister.
She always begged.

I prefer honesty.

-> END

=== sister_deny ===
#speaker: Maribel
Ah.
So you’re like her after all.

Very well.
I’ll wait.

Things trapped between worlds get impatient.
And impatience makes noise.

I’ll see you again.

-> END
