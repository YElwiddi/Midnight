EXTERNAL SetEventVar(varName, value)

VAR player_friendly = 0
VAR player_mean = 0
VAR player_scared = 0
VAR player_smart = 0
VAR player_brave = 0
VAR player_stupid = 0

=== start ===
#speaker: Harold Vunderbilt
Evening.
Didn’t expect anyone at the gate this late.

Name’s Harold Vonderbolt.
I need to step inside for a moment.

+ [Visiting hours are over. What’s your business here?]
    ~player_brave = player_brave + 1
    -> harold_business
+ [Can I help you with something, sir?]
    ~player_friendly = player_friendly + 1
    -> harold_polite

=== harold_business ===
#speaker: Harold Vunderbilt
Straight question.
I like that.

I’m here to see my brother.
Timothy.
Good man. Bad luck.

Didn’t think he’d end up here.
Funny how things go.

+ [How did you know Timothy?]
    ~player_smart = player_smart + 1
    -> harold_details
+ [Why come so late?]
    ~player_scared = player_scared + 1
    -> harold_late

=== harold_polite ===
#speaker: Harold Vunderbilt
Appreciate the courtesy.
Not much of it left these days.

I won’t take long.
Just paying respects.
Timothy wouldn’t forgive me if I didn’t.

+ [You’re being vague.]
    ~player_smart = player_smart + 1
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
    ~player_smart = player_smart + 1
    -> harold_partner
+ [Do you have any proof?]
    ~player_brave = player_brave + 1
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
Tim understood that.

-> harold_request

=== harold_id ===
#speaker: Harold Vunderbilt
Proof?
I don’t carry mementos.

And family names don’t mean much
once someone’s in the ground.
You should know that better than anyone.

But if it helps...
I wouldn’t be here if it didn’t matter.

-> harold_request

=== harold_request ===
#speaker: Harold Vunderbilt
So.
May I go in?

Just a few minutes.
I’ll stay out of your way.

+ [Alright. Be quick.]
    ~player_friendly = player_friendly + 1
    -> harold_allow
+ [No. I can’t allow it.]
    ~player_mean = player_mean + 1
    -> harold_deny

=== harold_allow ===
#speaker: Harold Vunderbilt
Good.
I knew you’d see reason.

Family doesn’t get many chances
to say goodbye properly.
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

 ~ SetEventVar("allowed_inside", false)

-> END