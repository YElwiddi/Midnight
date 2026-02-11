EXTERNAL SetEventVar(varName, value)
EXTERNAL SuspendDialogue()

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
-> ask

=== ask ===
May I come in?

+ [Fine. Go in. But be quick.]
     ~ SetEventVar("allowed_inside", true)
    -> sister_allow
+ [No. You’re not setting foot inside.]
     ~ SetEventVar("allowed_inside", false)
    -> sister_deny
    + [I have a few more questions.]
    -> more_questions
  + [Hold on, I'll be right back.]
      ~ SuspendDialogue()
    -> ask
    
    
    === more_questions ===
    Of course.
+ [What was your name again?]
    -> ask_name_again
+ [Why are you here?]
    -> why_here
+ [How can I trust you over your sister?]
    -> how_many
+ [Nevermind.]
-> ask

===how_many===
Ah, I see Tabitha has gotten quite the hold on you.
It is a decision for you to make, gravekeeper.
If you have felt uneasy since my sister's appearance, that is because the spirits here are enraged.
Tabitha does not know how to control the spirits she claims to speak to.
If you did indeed let her into these grounds, only I can undo her damage.
My sister dabbles in dark arts, but does not understand them. 
This is why I must come in.
+[I have other questions]
-> more_questions
+[Nevermind.]
-> ask
    
    === ask_name_again ===
    You're quite inquistive. My name is Maribel.
+[I have other questions]
-> more_questions
+[Nevermind.]
-> ask
    ===why_here===
    Likely for the same reason you are.
    Fate.
    +[Why did you choose this graveyard?]
    -> why_this
    +[Are you attempting to deceive me?]
    -> are_you_lying
    +[I have other questions.]
    -> more_questions
    +[Nevermind.]
    ->ask
    
    ===why_this===
    This graveyard has quite the allure to it.
    It is a noisy place. I was drawn to it.
    +[Are you attempting to deceive me?]
    -> are_you_lying
    +[I have other questions.]
    -> more_questions
    +[Nevermind.]
    ->ask
    
    ===are_you_lying===
    No. 
    I don't mean to cause any harm to you or the spirits you protect. 
    I feel them calling to me and I am obligated to respond.
        +[Why did you choose this graveyard?]
    -> why_this
        +[I have other questions.]
    -> more_questions
    +[Nevermind.]
    ->ask

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
