EXTERNAL SetEventVar(varName, value)
EXTERNAL SuspendDialogue()

VAR player_mean = 0
VAR player_scared = 0
VAR player_stupid = 0
VAR SpiritAngered = 0
VAR GraveRobberSetup = 0
VAR Sanity = 100
VAR GraveyardProtection = 100

=== start ===
#speaker: Harold Vunderbilt
Evening.
Didn’t expect anyone at the gate this late.

Name’s Harold Vanderbolt.
I need to step inside for a moment.

+ [Visiting hours are over. What’s your business here?]
    ~player_mean = player_mean + 1
    -> harold_business
+ [Can I help you with something, sir?]
    -> harold_polite

=== harold_business ===
#speaker: Harold Vunderbilt
Straight question.
I like that.

I’m here to see my brother.
Aaron.
Good man. Bad luck.

Didn’t think he’d end up here.
Funny how things go.

+ [How did you know Aaron?]
    -> harold_details
+ [Why come so late?]
    ~player_stupid = player_stupid + 1
    -> harold_late

=== harold_polite ===
#speaker: Harold Vunderbilt
Appreciate the courtesy.
Not much of it left these days.

I won’t take long.
Just paying respects.
Aaron wouldn’t forgive me if I didn’t.

+ [You’re being vague.]
    -> harold_details
+ [I’ll need more than that.]
    ~player_mean = player_mean + 1
    -> harold_pushback

=== harold_details ===
#speaker: Harold Vunderbilt
We grew up together.
Shared a name. Shared a roof.
Shared more mistakes than I care to count.

I owe him a moment.
Family has a way of demanding that.

+ [Your brother?]
~player_stupid = player_stupid + 1

    -> harold_partner
+ [Do you have any proof?]
~player_scared = player_scared + 1
    -> harold_id

=== harold_late ===
#speaker: Harold Vunderbilt
Privacy.
That’s all.

Some things are easier to say
when no one’s listening back.

I say what I need to say.
Then I leave.

-> harold_request

=== harold_pushback ===
#speaker: Harold Vunderbilt
Careful.
You’re doing your job.
So am I.

No trouble.
Just a brother standing where he has to.

Let’s not turn this into something tedious.

-> harold_request

=== harold_partner ===
#speaker: Harold Vunderbilt
Brother.
Older, if that matters.
Usually does.

We didn’t always agree.
But blood’s blood.
Aaron understood that.

-> harold_request

=== harold_id ===
#speaker: Harold Vunderbilt
Proof?
I don’t carry mementos.

And family names don’t mean much once someone’s in the ground.
You should know that better than anyone.

I wouldn’t be here if it didn’t matter.

-> harold_request

=== harold_request ===
#speaker: Harold Vunderbilt
So.

I just need a few minutes.
I’ll stay out of your way.
-> ask

===ask===

May I go in?

+ [Alright. Be quick.]
~player_stupid = player_stupid + 1
    ~GraveyardProtection = GraveyardProtection - 45
    -> harold_allow
+ [No. I can’t allow it.]
    -> harold_deny
+ [I have a few more questions.]
    -> more_questions
  + [Hold on, I'll be right back.]
      ~ SuspendDialogue()
    -> ask
    
    
=== more_questions ===
+ [What was your name again?]
    -> ask_name_again
+ [Who are you visiting?]
    -> who_visit
+ [How many people are in your family?]
    -> how_many
+ [Nevermind.]
-> ask

=== who_visit ===
I'm here to see Aaron.
+ [When did he die?]
-> when
+ [What was his cause of death?]
-> cause_of_death
+ [I have other questions.]
-> more_questions
+ [Nevermind.]
-> ask

===when===
June 27th, 1993.
+ [What was his cause of death?]
-> cause_of_death
+ [I have other questions.]
-> more_questions
+ [I have no other questions.]
-> ask
===cause_of_death===
He was found in his hotel room, on a trip abroad.
Had a gunshot wound out the back of his head.
Terrible way to go. I try not to think about it.
+ [When did he die?]
-> when
+ [I have other questions.]
-> more_questions
+ [I have no other questions.]
-> ask
=== how_many===
We didn't have much family left. It was just him and my nephew.
+ [I have more questions.]
-> more_questions
+ [I have no other questions.]
-> ask
=== ask_name_again ===
My name is Harold.
-> more_questions

=== harold_allow ===
#speaker: Harold Vunderbilt
Good.
I knew you’d see reason.

Family doesn’t get many chances to say goodbye properly.
I won’t forget this.

 ~ SetEventVar("allowed_inside", true)

-> END

=== harold_deny ===
#speaker: Harold Vunderbilt
That’s disappointing.

Still.
Rules are useful things.
Until they aren’t.

Give my brother a quiet night, then.
~player_stupid = player_stupid - 1
 ~ SetEventVar("allowed_inside", false)

-> END