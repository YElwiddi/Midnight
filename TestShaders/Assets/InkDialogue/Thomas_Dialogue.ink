VAR player_karma = 0

=== start ===
#speaker: Thomas
Greetings, traveler. I see you've found your way here.

+ [Who are you?]
    -> who_are_you
+ [What is this place?]
    -> about_place
+ [Get out of my way.]
    -> rude_response

=== who_are_you ===
#speaker: Thomas
I'm Thomas, a wanderer like yourself.
I've been traveling these roads for many years now.

+ [Nice to meet you, Thomas.]
    -> friendly_response
+ [I don't care. Move along.]
    -> dismissive_response

=== about_place ===
#speaker: Thomas
This? This is a crossroads of sorts.
Many paths lead here, but not all lead back out.

+ [That sounds ominous.]
    -> ominous_followup
+ [Whatever. I'll find my own way.]
    -> dismissive_response

=== ominous_followup ===
#speaker: Thomas
Heh, don't worry too much. Just be careful who you trust.
Perhaps we'll meet again, friend. Farewell.
~ player_karma = player_karma + 1
-> END

=== friendly_response ===
#speaker: Thomas
And you as well, friend. It's rare to meet someone polite these days.
May your journey be safe. Farewell.
~ player_karma = player_karma + 2
-> END

=== rude_response ===
#speaker: Thomas
My, my... such hostility.
Very well, I'll be on my way. But remember...
...karma has a way of finding us all.
~ player_karma = player_karma - 3
-> END

=== dismissive_response ===
#speaker: Thomas
Hmph. Suit yourself then.
I was going to offer some advice, but never mind.
~ player_karma = player_karma - 1
-> END
