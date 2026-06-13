pub const fn hash_string(input: &[u8]) -> u32 {
    let mut u_var1: u32;
    let mut c_var2;
    let mut i_var3: i32;
    let mut i_var4: i32;
    u_var1 = 0;
    if !input.is_empty() {
        i_var3 = 0;
        i_var4 = 0;
        let mut i = 0;
        while i < input.len() {
            c_var2 = input[i];
            u_var1 ^= ((c_var2 as i32).wrapping_sub(32) << (i_var3 & 0x1f)) as u32;
            if 0x18 < i_var3 {
                u_var1 ^= ((c_var2 as i32).wrapping_sub(32) >> (i_var4 & 0x1f)) as u32;
                if 0x1a < i_var3 {
                    i_var3 += -32;
                    i_var4 += 32;
                }
            }
            i_var3 = i_var3.wrapping_add(5);
            i_var4 = i_var4.wrapping_sub(5);
            i += 1;
        }
        if (u_var1 as i32) < 0 {
            u_var1 = u_var1.wrapping_neg();
        }
    }
    u_var1
}

pub const fn light_hash_string(input: &[u8]) -> u32 {
    if input.is_empty() || input[0] == 0 {
        return 0;
    }
    let mut u_var1: u32 = 0x1505;
    let mut i = 0;
    while i < input.len() {
        let c_var3 = input[i];
        let i_var2 = c_var3 as i32;
        u_var1 = i_var2.wrapping_add(u_var1.wrapping_mul(0x21) as i32) as u32;
        i += 1;
    }
    u_var1 & 0x7fffffff
}
