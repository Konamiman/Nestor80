CHPUT equ 00A2h

org 4000h
db 41h,42h
dw START
org 4010h
START:
	ld hl,hola
LOOP:
	ld a,(hl)
	or a
	jr z,INFILOOP
	call CHPUT
	inc hl
	jr LOOP
	ret
	jr c,LOOP

INFILOOP: jr INFILOOP

	.strenc 850
hola:
	dz "¡Cómo mola el Nestor80!\r\nY sin ñoñerías, ¿eh?"
	.strenc default

	org 7FFFh
	db 0
	end
