\ 1-wire from STM32F411RB
\ v1.2 07.05.2016
\ Based on source code:
\ - amForth, SPF4
\ Connection: PA0 - dq
\ us & ms words required !!!
\ compiletoflash
9 buffer: scratchpad
0 constant 1wDQ		\ 
1 1wDQ lshift constant 1wDQmask

: 1w.init %11 1wDQ 2* lshift gpioa_pupdr bic! ;
: 1w.output %01 1wDQ 2* lshift gpioa_moder bis! ;
: 1w.input %01 1wDQ 2* lshift gpioa_moder bic! ;
: 1w.out0 1wDQmask 16 lshift gpioa_bsrr ! ;
: 1w.out1 1wDQmask gpioa_bsrr ! ;
: 1w.in 1wDQmask gpioa_idr bit@ ;

: 1w.bitwr ( bit -- )
	1w.output 1w.out1 1w.out0 1 us
	%1 and 
	if 
		 6 us 1w.out1 64 us   
	else 
		64 us 1w.out0 6 us  
	then
	( 1 us)  1w.output 1w.out1 1w.input
;

: 1w.bitrd ( -- bit )
	1w.output 1w.out0 1 us 1w.out1 1w.input
	5 us  1w.in
	20 us  1w.output 1w.out1
;
\ single bit operations to read and write 1-wire bus
: 1W.BIT@ ( -- bit )    1w.bitrd 0<> 1 and  ; \ gives bit as 0 or 1
: 1W.BIT! ( bit -- )    0<> 1w.bitwr ;  \ takes bit as 0 or 1
: 1w-c! ( c -- )
	8 0 do dup 1w.bitwr 1 rshift loop
	drop
;

: 1w-c@ ( -- c )
	0 8 0 do 1 rshift 1w.bitrd if $80 or then loop
;

     \ block transmission functions

      : 1w!  ( addr n -- )
            over + swap
            do i c@ 1w-c! loop
            ;

      : 1w@  ( addr n -- )
            over + swap
            do 1w-c@ i c! loop
            ;
            
: 1w.reset ( -- f )
	\ 1w.init
	1w.output 1w.out0 470 us 1w.out1
	1w.input  60 us
	1w.in 0=  400 us
;

      
    \
    \ =============================================================
    \
    \
    \

 \     marker =dallas=
      

      $01  constant  id_DS2401    \ Silicon Serial Number 
      $10  constant  id_DS18S20   \ 1-Wire Parasite-Power Digital Thermometer
      $28  constant  id_DS18B20   \
      $22  constant  id_DS1822    \ Programmable Resolution 1-Wire Digital Thermometer
      
    \ DS1820/DS1822    ROM COMMANDS
      $F0  constant     SEARCH_ROM
      $33  constant     READ_ROM
      $55  constant     MATCH_ROM
      $CC  constant     SKIP_ROM
      $EC  constant     ALARM_SEARCH

    \ DS1820/DS1822    FUNCTION COMMANDS
      $44  constant     CONVERT_T
      $4E  constant     WRITE_SCRATCHPAD
      $BE  constant     READ_SCRATCHPAD
      $48  constant     COPY_SCRATCHPAD
      $B8  constant     RECALL_E2   
      $B4  constant     READ_POWER_SUPPLY

     \ ?????????????????? ???????????? ds18x2x

       0       \ rom
       1   --   1w_family
       6   --   1w_id#
       1   --   1w_crc
      constant /1w_rom

       0       \ scratchpad
       1   --   mem.tl
       1   --   mem.th
       1   --   mem.rth
       1   --   mem.rtl
       1   --   mem.config
       1   --   mem.5
       1   --   mem.6
       1   --   mem.7
       1   --   mem.crc
      constant /scratchpad
      
      /1w_rom buffer: @dallas
      /scratchpad buffer: @scratchpad
      
      
      \ ********************* crc8 **********************
$0      variable    msb#
$01     constant lsb#
$18     constant poly-8     \ x**8  + x**5  + x**4 + 1
$0	variable	polynom

: lsb  lsb# and ;
 
: crc8! poly-8  polynom ! $80 msb# ! ;
              

        : _crc   ( 's c  --- crc )
          8 0
            do
               over over xor lsb 
               if swap 
               polynom @ xor 1 rshift  msb# @ or
               else   swap 1 rshift
               then   swap 1 rshift
            loop 
            drop
            ;
: $crc8   ( a # --- crc )
 crc8!
            0 -rot over + swap
            do   i c@ _crc loop
            ;
            
\
    \
    \
    \ ****************************************************
    \
    \  1-Wire Search Algorithm
    \   (APPLICATION NOTE 187)
    \
    \ ****************************************************
    \ @dallas - ?????????? ?????? 64???????????????? ?????????? ( ?????????????? ?????????????? )
    \ @dallas - buffer for 64 bit key (network layer)

    \    anew  =1w-search=

    \   1w.f required
    \ crc8.f required
    \ ****************************************************
    \
    \ ??????????/???????????????????? ?????? 1-wire search algorithm
    \ flags/variables 1-wire search algorithm

          \ ???????????????????? ?????????? ?????????? ????????????????????
          \ previous search found the last
      $0 variable LastDeviceFlag
          \ ?????????? ???????? ?? ???????????????? ???????????? ???????????????????? ( ?????????????????? ) ??????????
          \ bit number that should appear (next) search
      $0 variable LastDiscrepancy
      $0 variable LastFamilyDiscrepancy
            \ ?????????? ????????, ???????? ?????? ?????????????? ?????????????????? 0 ?????? ????????????????????????
            \ bit number, which was recorded last 0 for mismatch
      $0 variable last_zero       \ variable #last-zero
      $0 variable search_direction   \ variable search-dir


    \ 1 constant lsb#
    \ ****************************************************
    \ @dallas /1w_rom 

        : rom_crc?  ( 's a n -- crc8 )
            @dallas /1w_rom  $crc8    \ 0= successful
            ;   


    \
        \ no device found then reset counters
        \ so next 'search' will be like a first
        : no_1w ( -- 0 )
            0  LastDiscrepancy !
            0  LastFamilyDiscrepancy !
            0  LastDeviceFlag !
            0
            ;

    \
        \ n =  ?????????? ???????? ?? @dallas ( 63..0 )
        \ # = ?????????? ???????? , a = ?????????? ??????????.
        : rom_bit#  ( n -- a # )
            $08  /mod @dallas + 
            swap lsb# 
            swap lshift
            ;
       
        \ ???????????????? ?? ??????????: ??????????????????, ??????????, ????????????
        \ operation with the bit: setting, resetting, reading
        : rom_bit+   ( n -- )
            rom_bit#
            over  c@ or
            swap  c!
            ;
     
        : rom_bit-   ( n -- )
            rom_bit# not
            over  c@ and
            swap  c!
            ;
            
        : rom_bit@   ( n -- b )
            rom_bit#
            swap  c@ and 
            ;

: 1w_search_p1
                   over =         if
                	drop
                     i LastDiscrepancy @ =  if
                     1 search_direction ! else
                     
                          i LastDiscrepancy @  >   if
                          0 search_direction !  else
                          i 1- rom_bit@
                          search_direction ! then  
                                        then
                                        
                     search_direction @ 0=   if
                         i dup last_zero !
                         9 <  if last_zero @ LastFamilyDiscrepancy ! then
                                             then
                                    else
                   ( bit ) search_direction !
                                    then
                          
                   search_direction @  if
                   i 1- rom_bit+ 1 1W.BIT!  ( 1w-1!) else
                   i 1- rom_bit- 0 1W.BIT! ( 1w-0!) then
;                
    \
    \ cmd - SEARCH_ROM or ALARM_SEARCH command

        : 1w_search ( cmd -- f ) 

            1w.reset  if

                LastDeviceFlag @ 0= if
               
                0 last_zero ! ( cmd ) 1w-c!
               
                $41 $01
                do
                   1W.BIT@ 1W.BIT@ \ 1wbit?

                   over over and if    \ ?????? ???????? ==1, ??????????
                   drop drop false
                  \ cr ." no devices"
                   unloop   exit then

					1w_search_p1
                loop
                
                last_zero @ dup LastDiscrepancy ! 0= if
                         true 	LastDeviceFlag !    then
                    
                LastDiscrepancy @ LastFamilyDiscrepancy @ = if
                            0 LastFamilyDiscrepancy !  then
                        
                                        rom_crc? 0= if
                                        true  exit  then

                                  else       
                       drop no_1w  \ cr 
                    ( ." last found" cr) then
                   
                             else       
                  drop no_1w \ cr
        \ ." not found" cr 
                         then
            ;
            
  \       
    \ ****************************************************
    \ ???????????????????????? ???????? 1-wire, ???????????????????? ?????????????? ??????????????????
    \ Network scanning 1-wire, construction of devices table
    \
    \
        0  variable 1w_last   \ ???????????????????? 1-wire ???????????????? ???? ???????? / the number of 1-wire devices on the bus 
       $10 variable max_1w   \ ??????-???? 1-wire ?????????????????? ( 16*8= 128???????? ) / number of 1-wire devices

      \ ?????????????? 1-wire ??????????????????
      \ Table 1-wire devices
      \ here dup max_1w /1w_rom * dup allot erase value 1w_devices

      \ here dup page-size dup allot erase value storage-buff
	max_1w @ /1w_rom * buffer: storage-buff
    \
    \ ****************************************************
    \
    \ ?????????????????????? 64?????? ???????????????????????????? ???? ???????????? @dallas
    \ ?? ?????????????? ??????????????????, i - ???????????? ???????????????????? ?? ??????????????
      : 1w_romt!   ( i -- )
          /1w_rom * 
           storage-buff + 
           @dallas swap 
          /1w_rom move
          ;

    \ ?????????????????????? 64?????? ???????????????????????????? ???? ?????????????? ??????????????????
    \ ?? ?????????? @dallas, i - ???????????? ???????????????????? ?? ??????????????
    \ copying the identifier of 64 bits into the table 
    \ buffer @dallas devices, i - the index in the table unit
      : 1w_romt@   ( i -- )
          /1w_rom * 
           storage-buff +
           @dallas 
          /1w_rom move
          ;      
          
 \
    \ ****************************************************
    \ 
    \ ???????????????????????? ????????, ???????????????????? ??????????????
    \ network scan, the construction of the table
      : 1w_scan

            no_1w  1w_last !

            SEARCH_ROM
            begin
            dup 1w_search 
            @dallas 7 $crc8 @dallas 1w_crc c@ = and
            if
                        1w_last @ dup 
                    1w_romt! 
            1+ 1w_last !
                          else  
            \ .1w_discovered 
            drop exit     then
         
            LastDeviceFlag  @
            until drop
            \ .1w_discovered 
            ;          

   	\
    \ ?????????? 1-wire ?????????????? ???? ??????????????
    \ choice of 1-wire device from the table
      : 1w_select ( i --  )
            1w.reset
            if
               
              1w_romt@ 
              MATCH_ROM  
              1w-c!
              @dallas /1w_rom 1w!
            else
              drop
            then
            ;


    \
    \ ???????????? ???????????? ???? ???????????????????? ?????????????? ?? ?????????????????? ?????????????????????? ??????????
    \ ?? ?????????? 1W_SCRATCHPAD
    \ reading data from the selected device with the checksum in the buffer 1W_SCRATCHPAD
      : 1w_read  ( i -- f )
            1w_select
            READ_SCRATCHPAD 
            1w-c!
            @scratchpad /scratchpad 1w@ 
            \ @scratchpad /scratchpad $crc8
            @scratchpad 8 $crc8 @scratchpad mem.crc c@  = if true else false then
            ; 

    \ ???????????? ????????????????????????????,
	\ start conversion
      : 1w_convert ( i-- )
            1w_select
            CONVERT_T
            1w-c!
\            begin 1 ms 0 1w-bit@ until
true 1w.input begin 1- dup 0= 1w.in or until \ Only externally powered DS18B20s
drop
            ;

    \ ??????????????????,???????????????????? ???????????????????? ?? @scratchpad
    \ measurement, read results in @scratchpad
    \ f=1 success

      : 1w_sensor@ ( i -- f )
            dup
            1w_convert
            1w_read 
            ;
            

: ds>dec 
dup 2/ 2/ 2/ 2/ 10 *
swap $f and 625 * 1000 / +
;

: temp>pad ( n -- adr n )
ds>dec
s>d swap over dabs <# # [char] . hold #s rot sign #>
;
\ compiletoram
\ Sample:
\ : .temp @scratchpad @ $ffff and temp>pad cr type ;
\ : t1 0 1w_sensor@ if .temp else cr ." Error!" then ;
\ : t2 1 1w_sensor@ if .temp else cr ." Error!" then ;
  
\ : test 1w.init storage-buff max_1w @ /1w_rom * 0 fill 1w_scan 1w_last @ cr ." devices:" . ;
