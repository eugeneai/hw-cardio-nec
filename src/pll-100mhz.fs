

\ f (VCO clock) = f (PLL clock input) * (PLLN/PLLM)
\ f (PLL general clock output) = F (VCO clock) / PLLP
\ f (USB, RNG und andere) = f (VCO clock) / PLLQ
\ uses 10-mmap.fs
\ compiletoflash
compiletoram
  1 24 lshift constant PLLON
  1 25 lshift constant PLLRDY
  1 22 lshift constant PLLSRC
  1 16 lshift constant HSEON
  1 17 lshift constant HSERDY



: 100MHz ( -- )
HSEON RCC RCC.CR bis!
begin ." HSERDY " HSERDY RCC RCC.CR bit@ until
  \ Set Flash waitstates !
  $104 FLASH.ACR !   \ 3 Waitstates for 120 MHz with more than 2.7 V Vcc, Prefetch buffer enabled.

  PLLSRC          \ HSE clock as 8 MHz source

  8  0 lshift or  \ PLLM Division factor for main PLL and audio PLL input clock
                  \ 8 MHz / 8 =  1 MHz. Divider before VCO. Frequency entering VCO to be between 1 and 2 MHz.

200  6 lshift or  \ PLLN Main PLL multiplication factor for VCO - between 192 and 432 MHz
                  \ 1 MHz * 200 = 200 MHz

  5 24 lshift or  \ PLLQ = 5, 200 MHz / 5 = 40 MHz

  0 16 lshift or  \ PLLP Division factor for main system clock
                  \ 0: /2  1: /4  2: /6  3: /8
                  \ 200 MHz / 2 = 100 MHz
  RCC RCC.PLLCFGR !

  PLLON RCC RCC.CR bis!
    \ Wait for PLL to lock:
    begin ." PLLRDY " PLLRDY RCC RCC.CR bit@ until

  2                 \ Set PLL as clock source
  %101 10 lshift or \ APB  Low speed prescaler (APB1) - Max 42 MHz ! Here 100/4 MHz = 25 MHz.
  %100 13 lshift or \ APB High speed prescaler (APB2) - Max 90 MHz ! Here 100/2 MHz = 50 MHz.
  RCC RCC.CFGR !

  \ $d9  USART2_BRR ! \ Set Baud rate divider for 115200 Baud at 25 MHz.
  \ $1b1  USART2_BRR ! \ Set Baud rate divider (27.1267) for 115200 Baud at 25 MHz.
  $364 USART2 USART.BRR ! \ Set Baud rate divider (27.1267) for 115200 Baud at 25 MHz.
;
: init100 100mhz cr ." Freq: 100 MHz" cr ;
compiletoram
